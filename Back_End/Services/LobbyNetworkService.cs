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

        public LobbyNetworkService(PlayerConnectionService playerConnections, ILogger<LobbyNetworkService> logger, ConnectionTicketService ticketService, PlayerPositionService playerPositionService, IMatchStore matchStore, GameBroadcastService gameBroadcastService)
        {
            playerConnectionService = playerConnections;
            this.logger = logger;
            this.ticketService = ticketService;
            this.playerPositionService = playerPositionService;
            this.matchStore = matchStore;
            this.gameBroadcastService = gameBroadcastService;
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

                    if (packetType != 1 || reader.AvailableBytes != 16)
                        return;

                    float x = reader.GetFloat();
                    float y = reader.GetFloat();
                    float z = reader.GetFloat();
                    float rotationY = reader.GetFloat();

                    if (!float.IsFinite(x) ||
                        !float.IsFinite(y) ||
                        !float.IsFinite(z) ||
                        !float.IsFinite(rotationY))
                    {
                        return;
                    }

                    playerPositionService.Update(playerId, x, y, z, rotationY);
                    BroadcastPlayerPosition(playerId, x, y, z, rotationY);
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

        private void BroadcastPlayerPosition(string playerId, float x, float y, float z, float rotationY)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)2);

            writer.Put(playerId);
            writer.Put(x);
            writer.Put(y);
            writer.Put(z);
            writer.Put(rotationY);

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
