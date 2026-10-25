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

        public LobbyNetworkService(PlayerConnectionService playerConnections, ILogger<LobbyNetworkService> logger, ConnectionTicketService ticketService, PlayerPositionService playerPositionService)
        {
            playerConnectionService = playerConnections;
            this.logger = logger;
            this.ticketService = ticketService;
            this.playerPositionService = playerPositionService;
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

                    if (reader.AvailableBytes != 17)
                        return;

                    byte packetType = reader.GetByte();

                    if (packetType != 1)
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
