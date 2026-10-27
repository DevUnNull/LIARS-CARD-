using System.Collections.Concurrent;
using Back_End.Models;

namespace Back_End.Infrastructure.MatchState
{
    public class InMemoryMatchStore : IMatchStore
    {
        private readonly ConcurrentDictionary<string, GameMatch> _matches = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ConcurrentDictionary<(string MatchId, Guid ActionId), ActionResultRecord> _actions = new();
        private readonly ConcurrentDictionary<(string MatchId, Guid PenaltyEventId), PenaltyAnimationSession> _penaltyAnimations = new();
        private readonly ConcurrentDictionary<(string MatchId, Guid PenaltyEventId), byte> _pendingPenaltyAnimations = new();
        private readonly ConcurrentDictionary<(string MatchId, int StateVersion), byte> _publishedMatchEndEvents = new();

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
            bool deleted = _matches.TryRemove(matchId, out _);

            foreach (var key in _actions.Keys.Where(key => key.MatchId == matchId))
            {
                _actions.TryRemove(key, out _);
            }

            foreach (var key in _penaltyAnimations.Keys.Where(key => key.MatchId == matchId))
            {
                _penaltyAnimations.TryRemove(key, out _);
                _pendingPenaltyAnimations.TryRemove(key, out _);
            }

            foreach (var key in _publishedMatchEndEvents.Keys.Where(key => key.MatchId == matchId))
            {
                _publishedMatchEndEvents.TryRemove(key, out _);
            }

            return deleted;
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

        public bool TryCreatePenaltyAnimation(PenaltyAnimationSession session)
        {
            var key = (session.MatchId, session.PenaltyEventId);
            if (!_penaltyAnimations.TryAdd(key, session))
            {
                return false;
            }

            if (session.Status == PenaltyAnimationStatus.Pending)
            {
                _pendingPenaltyAnimations.TryAdd(key, 0);
            }

            return true;
        }

        public PenaltyAnimationSession? GetPenaltyAnimation(string matchId, Guid penaltyEventId)
        {
            return _penaltyAnimations.TryGetValue((matchId, penaltyEventId), out var session)
                ? session
                : null;
        }

        public IReadOnlyList<PenaltyAnimationSession> GetPenaltyAnimations(string matchId)
        {
            return _penaltyAnimations
                .Where(entry => entry.Key.MatchId == matchId)
                .Select(entry => entry.Value)
                .ToArray();
        }

        public IReadOnlyList<(string MatchId, Guid PenaltyEventId)> GetPendingPenaltyAnimationIds()
        {
            return _pendingPenaltyAnimations.Keys
                .Select(key => (key.MatchId, key.PenaltyEventId))
                .ToArray();
        }

        public void SavePenaltyAnimation(PenaltyAnimationSession session)
        {
            var key = (session.MatchId, session.PenaltyEventId);
            if (!_penaltyAnimations.ContainsKey(key))
            {
                throw new InvalidOperationException("Penalty animation session does not exist.");
            }

            if (session.Status == PenaltyAnimationStatus.Pending)
            {
                _pendingPenaltyAnimations.TryAdd(key, 0);
            }
            else
            {
                _pendingPenaltyAnimations.TryRemove(key, out _);
            }
        }

        public bool TryMarkMatchEndedEventPublished(string matchId, int stateVersion) =>
            _publishedMatchEndEvents.TryAdd((matchId, stateVersion), 0);

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
