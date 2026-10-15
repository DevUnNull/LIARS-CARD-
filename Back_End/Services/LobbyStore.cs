using Back_End.Models;
using System.Collections.Concurrent;

namespace Back_End.Services
{
    public class LobbyStore
    {
        public ConcurrentDictionary<string, Lobby> Lobbies = new();
    }
}
