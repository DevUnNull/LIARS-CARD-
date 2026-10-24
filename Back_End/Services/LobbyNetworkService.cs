using LiteNetLib;

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

        public LobbyNetworkService(PlayerConnectionService playerConnections, ILogger<LobbyNetworkService> logger, ConnectionTicketService ticketService)
        {
            playerConnectionService = playerConnections;
            this.logger = logger;
            this.ticketService = ticketService;
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
                string? playerId = playerConnectionService.GetPlayerId(peer);

                playerConnectionService.RemoveByConnection(peer);

                logger.LogInformation("Disconnected: {ConnectionId}, PlayerId: {PlayerId}, Reason: {Reason}.", peer.Id, playerId, info.Reason);
            };

            listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) =>
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
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {

            } finally
            {
                server.Stop();
                playerConnectionService.Clear();
                logger.LogInformation("LiteNetLib stopped");
            }
        }
    }
}
