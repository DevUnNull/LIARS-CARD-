using Back_End.DTOs;
using Back_End.Models;

namespace Back_End.Services
{
    public interface IPenaltyAnimationService
    {
        PenaltyAnimationSession CreateSession(GameMatch match, Guid penaltyEventId, PenaltyResult penaltyResult);
        Task PublishStartedAsync(PenaltyAnimationSession session, CancellationToken cancellationToken);
        Task<PenaltyAnimationAckResponse> AcknowledgeAsync(
            string matchId,
            Guid penaltyEventId,
            string playerId,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<PenaltyAnimationSnapshot>> GetReconnectSnapshotAsync(
            string matchId,
            string playerId,
            CancellationToken cancellationToken);
        Task<bool> TryTimeoutAsync(
            string matchId,
            Guid penaltyEventId,
            CancellationToken cancellationToken);
    }
}
