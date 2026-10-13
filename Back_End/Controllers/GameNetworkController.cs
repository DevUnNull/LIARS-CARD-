using Back_End.Services;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Back_End.Controllers
{
    public class GameNetworkController : BackgroundService
    {
        private readonly ILogger<GameNetworkController> logger;
        private readonly PlayerService playerService;

        private readonly Dictionary<int, NetPeer> connections = new();

        public GameNetworkController(ILogger<GameNetworkController> logger, PlayerService playerService)
        {
            this.logger = logger;
            this.playerService = playerService;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new EventBasedNetListener();
            var server = new NetManager(listener);

            listener.ConnectionRequestEvent += request =>
            {
                request.AcceptIfKey("Liar");
            };

            listener.PeerConnectedEvent += HandleConnected;

            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                HandleDisconnected(peer);
            };

            listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                HandleReceived(peer, reader);
            };

            if (!server.Start(9050))
            {
                throw new InvalidOperationException(
                    "Can not open in port 9050.");
            }

            logger.LogInformation(
                "Openned port 9050.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    server.PollEvents();
                    await Task.Delay(15, stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {

            }
            finally
            {
                server.Stop();
                logger.LogInformation("Stop server.");
            }
        }

        private void HandleReceived(NetPeer peer, NetPacketReader reader)
        {
            try
            {
                if (reader.AvailableBytes < 1)
                    return;

                byte messageType = reader.GetByte();

                switch (messageType)
                {
                    case 1:
                        HandleMovement(peer, reader);
                        break;
                }
            }
            finally
            {
                reader.Recycle();
            }

        }

        private void HandleConnected(NetPeer peer)
        {
            if (!playerService.AddPlayer(peer.Id))
            {
                return;
            }

            connections.Add(peer.Id, peer);
            SendPlayerMessage(peer, 2, peer.Id);

            foreach (int otherId in playerService.GetPlayerIds())
            {
                if (otherId == peer.Id)
                {
                    continue;
                }

                SendPlayerMessage(peer, 3, otherId);

                if (connections.TryGetValue(otherId, out var otherPeer))
                {
                    SendPlayerMessage(otherPeer, 3, peer.Id);
                }
            }
        }

        private void HandleDisconnected(NetPeer peer)
        {
            connections.Remove(peer.Id);

            if (!playerService.RemovePlayer(peer.Id))
            {
                return;
            }

            foreach (var receiver in connections.Values)
            {
                SendPlayerMessage(receiver, 4, peer.Id);
            }
        }

        private void HandleMovement(NetPeer peer, NetPacketReader reader)
        {
            if (reader.AvailableBytes != 16)
                return;

            float x = reader.GetFloat();
            float y = reader.GetFloat();
            float z = reader.GetFloat();
            float yaw = reader.GetFloat();

            bool updated = playerService.UpdatePosition(
                peer.Id, x, y, z, yaw);

            //if (updated)
            //{
            //    logger.LogInformation(
            //        "Player {Id}: Position ({X}, {Y}, {Z}), Yaw {Yaw}",
            //        peer.Id, x, y, z, yaw);
            //}
        }

        private static void SendPlayerMessage(NetPeer receiver, byte type, int playerId)
        {
            var writer = new NetDataWriter();

            writer.Put(type);
            writer.Put(playerId);

            receiver.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }
}
