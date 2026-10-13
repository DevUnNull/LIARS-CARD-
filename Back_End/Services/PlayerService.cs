using Back_End.Models;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Back_End.Services
{
    public class PlayerService
    {
        private readonly Dictionary<int, PlayerState> players = new();
        private readonly ILogger<PlayerService> logger;

        public PlayerService(ILogger<PlayerService> logger)
        {
            this.logger = logger;
        }

        public bool AddPlayer(int playerId)
        {
            if (players.ContainsKey(playerId))
            {
                return false;
            }

            players.Add(playerId, new PlayerState(playerId));

            logger.LogInformation(
                "Player {Id} joined. Total: {Count}",
                playerId, players.Count);

            return true;
        }

        public bool RemovePlayer(int playerId)
        {
            if (!players.Remove(playerId))
            {
                return false;
            }

            logger.LogInformation(
                "Player {Id} left. Total: {Count}",
                playerId, players.Count);

            return true;
        }

        public int[] GetPlayerIds()
        {
            return players.Keys.ToArray();
        }

        public bool UpdatePosition(int playerId, float x, float y, float z, float yaw)
        {
            if (!players.TryGetValue(playerId, out var player))
            {
                return false;
            }

            player.SetPosition(x, y, z, yaw);
            return true;
        }
    }
}
