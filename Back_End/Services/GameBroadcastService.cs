using Back_End.Models;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;

namespace Back_End.Services
{
    /// <summary>
    /// Service đồng bộ các sự kiện âm thanh (3D Spatial SFX) và trạng thái phạt Russian Roulette qua UDP LiteNetLib.
    /// </summary>
    public class GameBroadcastService
    {
        private readonly PlayerConnectionService _playerConnectionService;
        private readonly PlayerPositionService _playerPositionService;
        private readonly ILogger<GameBroadcastService>? _logger;

        public GameBroadcastService(
            PlayerConnectionService playerConnectionService,
            PlayerPositionService playerPositionService,
            ILogger<GameBroadcastService>? logger = null)
        {
            _playerConnectionService = playerConnectionService;
            _playerPositionService = playerPositionService;
            _logger = logger;
        }

        /// <summary>
        /// Broadcast phát âm thanh SFX (Spatial Sound) tại vị trí người chơi tới tất cả người chơi trong trận đấu.
        /// Packet Type: 10
        /// Format: byte(10), string soundName, float x, float y, float z
        /// </summary>
        public void BroadcastAudio(GameMatch match, string soundName, string sourcePlayerId)
        {
            float x = 0f, y = 1.2f, z = 0f;

            if (_playerPositionService.TryGet(sourcePlayerId, out var pos) && pos != null)
            {
                x = pos.X;
                y = pos.Y;
                z = pos.Z;
            }
            else
            {
                var player = match.FindPlayer(sourcePlayerId);
                if (player != null)
                {
                    (x, y, z) = GetSeatCoordinates(player.SeatIndex);
                }
            }

            var writer = new NetDataWriter();
            writer.Put((byte)10);
            writer.Put(soundName);
            writer.Put(x);
            writer.Put(y);
            writer.Put(z);

            SendToMatchPlayers(match, writer);

            _logger?.LogInformation("BroadcastAudio: sound={SoundName}, playerId={PlayerId}, pos=({X:F2}, {Y:F2}, {Z:F2})",
                soundName, sourcePlayerId, x, y, z);
        }

        /// <summary>
        /// Broadcast kích hoạt trạng thái chờ người thua bóp cò súng (Russian Roulette).
        /// Packet Type: 11
        /// Format: byte(11), string penalizedPlayerId, string challengerPlayerId, bool challengeSucceeded
        /// </summary>
        public void BroadcastPenaltyWaitingForTrigger(
            GameMatch match,
            string penalizedPlayerId,
            string challengerPlayerId,
            bool challengeSucceeded)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)11);
            writer.Put(penalizedPlayerId);
            writer.Put(challengerPlayerId);
            writer.Put(challengeSucceeded);

            SendToMatchPlayers(match, writer);

            _logger?.LogInformation(
                "BroadcastPenaltyWaitingForTrigger: penalized={PenalizedId}, challenger={ChallengerId}, succeeded={Succeeded}",
                penalizedPlayerId, challengerPlayerId, challengeSucceeded);
        }

        /// <summary>
        /// Broadcast kết quả bóp cò súng Nga (đoàng/nổ hay rỗng cạch) tới toàn bộ người chơi trong phòng.
        /// Packet Type: 12
        /// Format: byte(12), string penalizedPlayerId, bool isBulletHit, bool survived, bool eliminated, string nextPhase, string winnerPlayerId
        /// </summary>
        public void BroadcastTriggerResult(
            GameMatch match,
            string penalizedPlayerId,
            bool isBulletHit,
            bool survived,
            bool eliminated,
            string nextPhase,
            string? winnerPlayerId)
        {
            var writer = new NetDataWriter();
            writer.Put((byte)12);
            writer.Put(penalizedPlayerId);
            writer.Put(isBulletHit);
            writer.Put(survived);
            writer.Put(eliminated);
            writer.Put(nextPhase);
            writer.Put(winnerPlayerId ?? string.Empty);

            SendToMatchPlayers(match, writer);

            _logger?.LogInformation(
                "BroadcastTriggerResult: victim={VictimId}, isHit={Hit}, survived={Survived}, nextPhase={Phase}, winner={Winner}",
                penalizedPlayerId, isBulletHit, survived, nextPhase, winnerPlayerId);
        }

        private void SendToMatchPlayers(GameMatch match, NetDataWriter writer)
        {
            foreach (var player in match.Players)
            {
                var peer = _playerConnectionService.GetConnection(player.PlayerId);
                if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                {
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        private static (float x, float y, float z) GetSeatCoordinates(int seatIndex)
        {
            return seatIndex switch
            {
                0 => (0f, 1.2f, -1.2f),
                1 => (1.2f, 1.2f, 0f),
                2 => (0f, 1.2f, 1.2f),
                3 => (-1.2f, 1.2f, 0f),
                _ => (0f, 1.2f, 0f)
            };
        }
    }
}
