using Back_End.Models;

namespace Back_End.Infrastructure.MatchState
{
    public class ActionResultRecord
    {
        public Guid ActionId { get; set; }
        public string MatchId { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public object Result { get; set; } = new();
        public int StateVersion { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public interface IMatchStore
    {
        GameMatch? GetMatch(string matchId);
        void SaveMatch(GameMatch match);
        bool DeleteMatch(string matchId);
        Task<IDisposable> LockMatchAsync(string matchId);
        ActionResultRecord? GetActionResult(string matchId, Guid actionId);
        void SaveActionResult(string matchId, Guid actionId, string payloadHash, object result, int stateVersion);
        bool TryCreatePenaltyAnimation(PenaltyAnimationSession session);
        PenaltyAnimationSession? GetPenaltyAnimation(string matchId, Guid penaltyEventId);
        IReadOnlyList<PenaltyAnimationSession> GetPenaltyAnimations(string matchId);
        IReadOnlyList<(string MatchId, Guid PenaltyEventId)> GetPendingPenaltyAnimationIds();
        void SavePenaltyAnimation(PenaltyAnimationSession session);
        bool TryMarkMatchEndedEventPublished(string matchId, int stateVersion);
    }
}
