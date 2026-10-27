using Back_End.Data;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;

namespace Back_End.Services
{
    public sealed class MatchResultService : IMatchResultService
    {
        private readonly IMatchStore _matchStore;
        private readonly IMatchResultRepository _repository;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MatchResultService> _logger;

        public MatchResultService(
            IMatchStore matchStore,
            IMatchResultRepository repository,
            TimeProvider timeProvider,
            ILogger<MatchResultService> logger)
        {
            _matchStore = matchStore;
            _repository = repository;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<MatchResultResponse> PersistCompletedMatchAsync(
            string matchId,
            CancellationToken cancellationToken,
            bool isRetry = false)
        {
            MatchResultDocument snapshot;
            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId);
                if (match == null)
                {
                    throw GameRuleException.MatchNotFound();
                }

                if (match.Phase != RoundPhase.MatchEnded)
                {
                    throw GameRuleException.InvalidPhase(
                        "Không thể lưu kết quả khi trận đấu chưa kết thúc.",
                        match.StateVersion);
                }

                snapshot = CreateSnapshot(match, _timeProvider.GetUtcNow());
            }

            try
            {
                bool inserted = await _repository.TryInsertAsync(snapshot, cancellationToken);
                if (inserted)
                {
                    return ToResponse(snapshot);
                }

                var existing = await _repository.GetByMatchIdAsync(matchId, cancellationToken);
                if (existing == null)
                {
                    throw new MatchResultPersistenceException(
                        "Match result duplicate could not be verified.",
                        new InvalidOperationException("The existing result was not found after a duplicate insert."));
                }

                if (!HasSameFinalResult(existing, snapshot))
                {
                    _logger.LogError(
                        "Immutable match result conflict for {MatchId} at state version {StateVersion}; retry={IsRetry}.",
                        matchId,
                        snapshot.FinalStateVersion,
                        isRetry);
                    throw new MatchResultConflictException(
                        "A different final result is already stored for this match.");
                }

                return ToResponse(existing);
            }
            catch (MatchResultConflictException)
            {
                throw;
            }
            catch (MatchResultPersistenceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to persist match result for {MatchId} at state version {StateVersion}; retry={IsRetry}.",
                    matchId,
                    snapshot.FinalStateVersion,
                    isRetry);
                throw new MatchResultPersistenceException(
                    "The final match result could not be persisted.",
                    exception);
            }
        }

        public async Task<MatchResultResponse?> GetForParticipantAsync(
            string matchId,
            string playerId,
            CancellationToken cancellationToken)
        {
            try
            {
                var document = await _repository.GetByMatchIdAsync(matchId, cancellationToken);
                if (document == null ||
                    !document.Players.Any(player =>
                        string.Equals(player.PlayerId, playerId, StringComparison.Ordinal)))
                {
                    return null;
                }

                return ToResponse(document);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Failed to query match result for {MatchId}.",
                    matchId);
                throw new MatchResultPersistenceException(
                    "The match result could not be queried.",
                    exception);
            }
        }

        private MatchResultDocument CreateSnapshot(GameMatch match, DateTimeOffset endedAtUtc) =>
            new()
            {
                MatchId = match.MatchId,
                WinnerPlayerId = match.WinnerPlayerId,
                Players = match.Players
                    .OrderBy(player => player.SeatIndex)
                    .Select(player => new MatchResultPlayerDocument
                    {
                        PlayerId = player.PlayerId,
                        DisplayName = player.DisplayName,
                        SeatIndex = player.SeatIndex,
                        IsAliveAtEnd = player.IsAlive,
                        IsWinner = player.PlayerId == match.WinnerPlayerId,
                        RemainingCardCountAtEnd = player.RemainingCardCount
                    })
                    .ToList(),
                MatchCreatedAtUtc = DateTime.SpecifyKind(match.CreatedAt, DateTimeKind.Utc),
                EndedAtUtc = endedAtUtc.UtcDateTime,
                FinalStateVersion = match.StateVersion
            };

        private static bool HasSameFinalResult(
            MatchResultDocument existing,
            MatchResultDocument candidate) =>
            existing.MatchId == candidate.MatchId &&
            existing.WinnerPlayerId == candidate.WinnerPlayerId &&
            existing.FinalStateVersion == candidate.FinalStateVersion &&
            existing.MatchCreatedAtUtc == candidate.MatchCreatedAtUtc &&
            existing.Players.Count == candidate.Players.Count &&
            existing.Players.OrderBy(player => player.SeatIndex)
                .SequenceEqual(candidate.Players.OrderBy(player => player.SeatIndex), MatchResultPlayerComparer.Instance);

        private static MatchResultResponse ToResponse(MatchResultDocument document) =>
            new(
                document.MatchId,
                document.WinnerPlayerId,
                document.Players
                    .OrderBy(player => player.SeatIndex)
                    .Select(player => new MatchResultPlayerResponse(
                        player.PlayerId,
                        player.DisplayName,
                        player.SeatIndex,
                        player.IsAliveAtEnd,
                        player.IsWinner,
                        player.RemainingCardCountAtEnd))
                    .ToArray(),
                new DateTimeOffset(DateTime.SpecifyKind(document.MatchCreatedAtUtc, DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind(document.EndedAtUtc, DateTimeKind.Utc)),
                document.FinalStateVersion);

        private sealed class MatchResultPlayerComparer : IEqualityComparer<MatchResultPlayerDocument>
        {
            public static MatchResultPlayerComparer Instance { get; } = new();

            public bool Equals(MatchResultPlayerDocument? left, MatchResultPlayerDocument? right) =>
                left != null &&
                right != null &&
                left.PlayerId == right.PlayerId &&
                left.DisplayName == right.DisplayName &&
                left.SeatIndex == right.SeatIndex &&
                left.IsAliveAtEnd == right.IsAliveAtEnd &&
                left.IsWinner == right.IsWinner &&
                left.RemainingCardCountAtEnd == right.RemainingCardCountAtEnd;

            public int GetHashCode(MatchResultPlayerDocument player) =>
                HashCode.Combine(
                    player.PlayerId,
                    player.DisplayName,
                    player.SeatIndex,
                    player.IsAliveAtEnd,
                    player.IsWinner,
                    player.RemainingCardCountAtEnd);
        }
    }
}
