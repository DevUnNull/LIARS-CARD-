using LiteNetLib;

namespace Back_End.Services
{
    public class PlayerConnectionService
    {
        private Dictionary<string, NetPeer> players = new();
        private readonly object lockObj = new();

        public bool Add(string playerID, NetPeer peer)
        {
            if (string.IsNullOrWhiteSpace(playerID))
            {
                return false;
            }

            lock (lockObj)
            {
                if (players.ContainsKey(playerID))
                {
                    return false;
                }

                if (players.Values.Any(p => ReferenceEquals(p, peer)))
                {
                    return false;
                }

                players.Add(playerID, peer);
                return true;
            }
        }

        public void RemoveByConnection(NetPeer peer)
        {
            lock (lockObj)
            {
                string? playerId = null;

                foreach (var entry in players)
                {
                    if (ReferenceEquals(entry.Value, peer))
                    {
                        playerId = entry.Key;
                        break;
                    }
                }

                if (playerId != null)
                {
                    players.Remove(playerId);
                }
            }
        }

        public NetPeer? GetConnection(string playerId)
        {
            lock (lockObj)
            {
                return players.TryGetValue(playerId, out var peer) ? peer : null;
            }
        }

        public string? GetPlayerId(NetPeer peer)
        {
            lock (lockObj)
            {
                foreach (var entry in players)
                {
                    if (ReferenceEquals(entry.Value, peer))
                    {
                        return entry.Key;
                    }
                }

                return null;
            }
        }

        public KeyValuePair<string, NetPeer>[] GetAll()
        {
            lock (lockObj)
            {
                return players.ToArray();
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
