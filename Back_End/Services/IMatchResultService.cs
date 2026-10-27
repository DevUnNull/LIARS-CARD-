using Back_End.DTOs;

namespace Back_End.Services
{
    public interface IMatchResultService
    {
        Task<MatchResultResponse> PersistCompletedMatchAsync(
            string matchId,
            CancellationToken cancellationToken,
            bool isRetry = false);

        Task<MatchResultResponse?> GetForParticipantAsync(
            string matchId,
            string playerId,
            CancellationToken cancellationToken);
    }
}
