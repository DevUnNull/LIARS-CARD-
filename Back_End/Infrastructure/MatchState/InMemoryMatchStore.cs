using System.Collections.Concurrent;
using Back_End.Models;

namespace Back_End.Infrastructure.MatchState
{
    public class InMemoryMatchStore : IMatchStore
    {
        private readonly ConcurrentDictionary<string, GameMatch> _matches = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ConcurrentDictionary<(string MatchId, Guid ActionId), ActionResultRecord> _actions = new();

        public GameMatch? GetMatch(string matchId)
        {
            if (_matches.TryGetValue(matchId, out var match))
            {
                return match;
            }
            return null;
        }

        public void SaveMatch(GameMatch match)
        {
            match.UpdatedAt = DateTime.UtcNow;
            _matches[match.MatchId] = match;
        }

        public bool DeleteMatch(string matchId)
        {
            return _matches.TryRemove(matchId, out _);
        }

        public async Task<IDisposable> LockMatchAsync(string matchId)
        {
            var semaphore = _locks.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            return new Releaser(semaphore);
        }

        public ActionResultRecord? GetActionResult(string matchId, Guid actionId)
        {
            if (_actions.TryGetValue((matchId, actionId), out var record))
            {
                return record;
            }
            return null;
        }

        public void SaveActionResult(string matchId, Guid actionId, string payloadHash, object result, int stateVersion)
        {
            _actions[(matchId, actionId)] = new ActionResultRecord
            {
                MatchId = matchId,
                ActionId = actionId,
                PayloadHash = payloadHash,
                Result = result,
                StateVersion = stateVersion,
                CreatedAt = DateTime.UtcNow
            };
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public Releaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _semaphore.Release();
                    _disposed = true;
                }
            }
        }
    }
}
