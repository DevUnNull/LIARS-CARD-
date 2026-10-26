// Kiểm tra tính hợp lệ của transition

namespace LiarsCard.Penalty
{
    public static class PenaltyStateMachine
    {
        public static bool CanTransition(PenaltyState current, PenaltyState next)
        {
            return current switch
            {
                PenaltyState.Pending => next == PenaltyState.Animating,

                PenaltyState.Animating => next == PenaltyState.Resolving,

                PenaltyState.Resolving => next == PenaltyState.Completed,

                PenaltyState.Completed => false,
                _ => false
            };
        }
    }
}
