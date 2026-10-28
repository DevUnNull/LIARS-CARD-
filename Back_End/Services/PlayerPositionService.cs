using Back_End.DTOs;

namespace Back_End.Services
{
    public class PlayerPositionService
    {
        private readonly Dictionary<string, PlayerPosition> players = new();
        private readonly object lockObj = new();

        public void Update(
            string playerId,
            float x,
            float y,
            float z,
            float rotationY)
        {
            lock (lockObj)
            {
                players[playerId] = new PlayerPosition(
                    playerId, x, y, z, rotationY);
            }
        }

        public PlayerPosition[] GetAll()
        {
            lock (lockObj)
            {
                return players.Values.ToArray();
            }
        }

        public bool TryGet(string playerId, out PlayerPosition? pos)
        {
            lock (lockObj)
            {
                return players.TryGetValue(playerId, out pos);
            }
        }

        public void Remove(string playerId)
        {
            lock (lockObj)
            {
                players.Remove(playerId);
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                players.Clear();
            }
        }
    }
}
