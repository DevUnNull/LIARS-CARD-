using LiteNetLib;

namespace Back_End
{
    public class GameNetworkService : BackgroundService
    {
        private readonly ILogger<GameNetworkService> _logger;

        public GameNetworkService(ILogger<GameNetworkService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new EventBasedNetListener();
            var server = new NetManager(listener);

            listener.ConnectionRequestEvent += request =>
            {
                request.AcceptIfKey("Liar");
            };

            listener.PeerConnectedEvent += peer =>
            {
                _logger.LogInformation(
                    "Client connected: {PeerId}", peer.Id);
            };

            listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                try
                {
                    byte messageType = reader.GetByte();

                    switch (messageType)
                    {
                        case 1:
                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            float z = reader.GetFloat();
                            float yaw = reader.GetFloat();

                            //_logger.LogInformation(
                            //    "Player {Id}: Position ({X}, {Y}, {Z}), Yaw {Yaw}",
                            //    peer.Id, x, y, z, yaw);
                            break;
                        default:
                            break;
                    }
                }
                finally
                {
                    reader.Recycle();
                }
            };

            if (!server.Start(9050))
            {
                throw new InvalidOperationException(
                    "Can not open in port 9050.");
            }

            _logger.LogInformation(
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
                _logger.LogInformation("Stop server.");
            }
        }
    }
}
