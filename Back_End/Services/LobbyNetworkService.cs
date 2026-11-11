using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Back_End.Services
{
    public class LobbyNetworkService : BackgroundService
    {
        private const int PORT = 9050;
        private const int MAX_CONNECTIONS = 100;
        private const string CONNECTION_KEY = "LiarsCard";

        private PlayerConnectionService playerConnectionService;
        private ILogger<LobbyNetworkService> logger;
        private ConnectionTicketService ticketService;
        private PlayerPositionService playerPositionService;
        private IMatchStore matchStore;
        private GameBroadcastService gameBroadcastService;
        private JukeboxService jukebox;

        // Skin đang mặc của từng người (chỉ truy cập trên luồng PollEvents)
        private readonly Dictionary<string, byte> skins = new();
        private const byte MAX_SKIN_ID = 63;

        public LobbyNetworkService(PlayerConnectionService playerConnections, ILogger<LobbyNetworkService> logger, ConnectionTicketService ticketService, PlayerPositionService playerPositionService, IMatchStore matchStore, GameBroadcastService gameBroadcastService, JukeboxService jukebox)
        {
            playerConnectionService = playerConnections;
            this.logger = logger;
            this.ticketService = ticketService;
            this.playerPositionService = playerPositionService;
            this.matchStore = matchStore;
            this.gameBroadcastService = gameBroadcastService;
            this.jukebox = jukebox;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new EventBasedNetListener();
            var server = new NetManager(listener);

            listener.ConnectionRequestEvent += request =>
            {
                if (server.ConnectedPeersCount >= MAX_CONNECTIONS)
                {
                    request.Reject();
                    return;
                }

                string key;
                string ticket;

                try
                {
                    key = request.Data.GetString(32);
                    ticket = request.Data.GetString(128);
                }
                catch (Exception)
                {
                    request.Reject();
                    return;
                }

                if (key != CONNECTION_KEY)
                {
                    request.Reject();
                    return;
                }

                string? playerId = ticketService.Consume(ticket);

                if (playerId == null)
                {
                    request.Reject();
                    return;
                }

                if (playerConnectionService.GetConnection(playerId) != null)
                {
                    request.Reject();
                    return;
                }

                var peer = request.Accept();

                if (peer == null)
                {
                    return;
                }

                bool added = playerConnectionService.Add(playerId, peer);

                if (!added)
                {
                    peer.Disconnect();
                    return;
                }

                // Người mới vào cần biết skin của những người đang có mặt
                foreach (var entry in skins)
                    peer.Send(SkinPacket(entry.Key, entry.Value), DeliveryMethod.ReliableOrdered);

                // Tên hiển thị: người mới nhận tên mọi người, mọi người nhận tên người mới
                var myName = NamePacket(playerId);
                peer.Send(myName, DeliveryMethod.ReliableOrdered);   // tên chính mình (hiện trong gương)
                foreach (var entry in playerConnectionService.GetAll())
                {
                    if (entry.Key == playerId || entry.Value.ConnectionState != ConnectionState.Connected)
                        continue;
                    peer.Send(NamePacket(entry.Key), DeliveryMethod.ReliableOrdered);
                    entry.Value.Send(myName, DeliveryMethod.ReliableOrdered);
                }

                // ...và bài Jukebox đang phát để nghe chung từ đúng đoạn đó
                peer.Send(jukebox.StatePacket(), DeliveryMethod.ReliableOrdered);

                logger.LogInformation(
                    "Authenticated: PlayerId {PlayerId}, ConnectionId {ConnectionId}",
                    playerId,
                    peer.Id);
            };

            listener.PeerConnectedEvent += peer =>
            {
                logger.LogInformation(
                    "Client connected: {ConnectionId}",
                    peer.Id);
            };

            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                string? playerId =
                    playerConnectionService.GetPlayerId(peer);

                playerConnectionService.RemoveByConnection(peer);

                if (playerId != null)
                {
                    playerPositionService.Remove(playerId);
                    skins.Remove(playerId);

                    var writer = new NetDataWriter();
                    writer.Put((byte)3);
                    writer.Put(playerId);

                    foreach (var entry in playerConnectionService.GetAll())
                    {
                        NetPeer targetPeer = entry.Value;

                        if (targetPeer.ConnectionState != ConnectionState.Connected)
                            continue;

                        targetPeer.Send(writer, DeliveryMethod.ReliableOrdered);
                    }
                }

                logger.LogInformation(
                    "Disconnected: {ConnectionId}, PlayerId: {PlayerId}, Reason: {Reason}",
                    peer.Id, playerId, info.Reason);
            };

            listener.NetworkReceiveEvent +=
            (peer, reader, channel, deliveryMethod) =>
            {
                try
                {
                    string? playerId =
                        playerConnectionService.GetPlayerId(peer);

                    if (playerId == null)
                    {
                        peer.Disconnect();
                        return;
                    }

                    if (reader.AvailableBytes < 1)
                        return;

                    byte packetType = reader.GetByte();

                    if (packetType == 4)
                    {
                        HandleGunPose(playerId, reader);
                        return;
                    }

                    if (packetType == 5)
                    {
                        HandleHandAction(playerId, reader);
                        return;
                    }

                    if (packetType == 6)
                    {
                        HandleSkin(playerId, reader);
                        return;
                    }

                    if (packetType == 7)
                    {
                        HandleJukebox(reader);
                        return;
                    }

                    // 16 byte = client cũ, 24 byte = kèm góc đầu (headYaw, headPitch)
                    if (packetType != 1 || (reader.AvailableBytes != 16 && reader.AvailableBytes != 24))
                        return;

                    float x = reader.GetFloat();
                    float y = reader.GetFloat();
                    float z = reader.GetFloat();
                    float rotationY = reader.GetFloat();
                    float headYaw = reader.AvailableBytes >= 8 ? reader.GetFloat() : 0f;
                    float headPitch = reader.AvailableBytes >= 4 ? reader.GetFloat() : 0f;

                    if (!float.IsFinite(x) ||
                        !float.IsFinite(y) ||
                        !float.IsFinite(z) ||
                        !float.IsFinite(rotationY) ||
                        !float.IsFinite(headYaw) ||
                        !float.IsFinite(headPitch))
                    {
                        return;
                    }

                    playerPositionService.Update(playerId, x, y, z, rotationY);
                    BroadcastPlayerPosition(playerId, x, y, z, rotationY,
                        Math.Clamp(headYaw, -120f, 120f), Math.Clamp(headPitch, -90f, 90f));
                }
                finally
                {
                    reader.Recycle();
                }
            };

            try
            {
                if (!server.Start(PORT))
                {
                    throw new InvalidOperationException($"Cannot open {PORT}.");
                }

                logger.LogInformation($"LiteNetLib port opened");

                while (!stoppingToken.IsCancellationRequested)
                {
                    server.PollEvents();

                    await Task.Delay(15, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {

            }
            finally
            {
                server.Stop();
                playerConnectionService.Clear();
                playerPositionService.Clear();
                skins.Clear();
                logger.LogInformation("LiteNetLib stopped");
            }
        }

        /// <summary>
        /// Client -> Server, Packet Type 4: byte stage, float pitch.
        /// Chỉ chấp nhận từ đúng người đang bị phạt trong lúc chờ bóp cò; hợp lệ thì chuyển tiếp (packet 13) cho cả bàn.
        /// </summary>
        private void HandleGunPose(string playerId, NetPacketReader reader)
        {
            if (reader.AvailableBytes != 5)
                return;

            byte stage = reader.GetByte();
            float pitch = reader.GetFloat();

            if (stage < 1 || stage > 2 || !float.IsFinite(pitch))
                return;

            var match = matchStore.FindActiveMatchByPlayer(playerId);

            if (match == null ||
                match.Phase != RoundPhase.PenaltyResolution ||
                match.PenaltyResolved ||
                match.PenalizedPlayerId != playerId)
            {
                return;
            }

            gameBroadcastService.BroadcastGunPose(match, playerId, stage, Math.Clamp(pitch, -90f, 90f));
        }

        /// <summary>
        /// Client -> Server, Packet Type 5: byte action, byte count (0 = hạ bài, 1 = nhấc bài xem, 2 = đánh bài).
        /// Chỉ là hiệu ứng hiển thị: chỉ chuyển tiếp (packet 14) khi người gửi đang trong trận chưa kết thúc;
        /// đánh bài (2) phải là 1..3 lá.
        /// </summary>
        private void HandleHandAction(string playerId, NetPacketReader reader)
        {
            if (reader.AvailableBytes != 2)
                return;

            byte action = reader.GetByte();
            byte count = reader.GetByte();

            if (action > 2 || (action == 2 && (count < 1 || count > 3)))
                return;

            var match = matchStore.FindActiveMatchByPlayer(playerId);

            // Không kiểm tra lượt: client gửi sau khi REST đánh bài thành công nên lượt có thể đã chuyển
            if (match == null)
                return;

            gameBroadcastService.BroadcastHandAction(match, playerId, action, action == 2 ? count : (byte)0);
        }

        /// <summary>
        /// Client -> Server, Packet Type 6: byte skinId. Lưu lại và chuyển tiếp (packet 15) cho mọi người khác.
        /// Server không biết danh sách skin, chỉ chặn giá trị quá lớn; client tự bỏ qua ID không có.
        /// </summary>
        private void HandleSkin(string playerId, NetPacketReader reader)
        {
            if (reader.AvailableBytes != 1)
                return;

            byte skinId = reader.GetByte();

            if (skinId > MAX_SKIN_ID || (skins.TryGetValue(playerId, out byte old) && old == skinId))
                return;

            skins[playerId] = skinId;

            var writer = SkinPacket(playerId, skinId);

            foreach (var entry in playerConnectionService.GetAll())
            {
                if (entry.Key == playerId || entry.Value.ConnectionState != ConnectionState.Connected)
                    continue;

                entry.Value.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        /// <summary>
        /// Client -> Server, Packet Type 7: byte cmd (JukeboxService.Command), riêng Ended kèm int index.
        /// JukeboxService tự broadcast packet 16 khi bài đổi.
        /// </summary>
        private void HandleJukebox(NetPacketReader reader)
        {
            if (reader.AvailableBytes < 1)
                return;

            var cmd = (JukeboxService.Command)reader.GetByte();

            if (cmd == JukeboxService.Command.Ended)
            {
                if (reader.AvailableBytes == 4)
                    jukebox.Handle(cmd, reader.GetInt());
            }
            else if (reader.AvailableBytes == 0)
            {
                jukebox.Handle(cmd);   // lệnh lạ bị Handle bỏ qua
            }
        }

        /// <summary>Server -> Client, Packet Type 17: string playerId, string displayName.</summary>
        private static NetDataWriter NamePacket(string playerId)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)17);
            writer.Put(playerId);
            writer.Put(PlayerNames.Get(playerId) ?? playerId, 64);
            return writer;
        }

        private static NetDataWriter SkinPacket(string playerId, byte skinId)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)15);
            writer.Put(playerId);
            writer.Put(skinId);
            return writer;
        }

        private void BroadcastPlayerPosition(string playerId, float x, float y, float z, float rotationY, float headYaw, float headPitch)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)2);

            writer.Put(playerId);
            writer.Put(x);
            writer.Put(y);
            writer.Put(z);
            writer.Put(rotationY);
            writer.Put(headYaw);
            writer.Put(headPitch);

            foreach (var entry in playerConnectionService.GetAll())
            {
                if (entry.Key == playerId)
                    continue;

                NetPeer targetPeer = entry.Value;

                if (targetPeer.ConnectionState != ConnectionState.Connected)
                    continue;

                targetPeer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }
}
