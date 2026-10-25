using Back_End.Models;

namespace Back_End.Rules
{
    public class TurnStateMachine
    {
        /// <summary>
        /// Gets the initial turn player for a starting round.
        /// Finds the first alive player with cards, optionally starting search from a specific seat.
        /// </summary>
        public MatchPlayer? GetInitialTurnPlayer(GameMatch match, int preferredSeatIndex = 0)
        {
            var aliveWithCards = match.AlivePlayers
                .Where(p => p.RemainingCardCount > 0)
                .OrderBy(p => (p.SeatIndex - preferredSeatIndex + match.Players.Count) % match.Players.Count)
                .ToList();

            return aliveWithCards.FirstOrDefault();
        }

        /// <summary>
        /// Determines the next player eligible to take a turn.
        /// Skips eliminated players (IsAlive == false).
        /// Skips players who have 0 remaining cards in the current round.
        /// </summary>
        public MatchPlayer? GetNextTurnPlayer(GameMatch match, string currentPlayerId)
        {
            if (match.Players.Count == 0)
            {
                return null;
            }

            int currentIndex = match.Players.FindIndex(p => p.PlayerId == currentPlayerId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            // Check candidates in clockwise order
            for (int step = 1; step <= match.Players.Count; step++)
            {
                int nextIndex = (currentIndex + step) % match.Players.Count;
                var candidate = match.Players[nextIndex];

                // Must be alive and have at least 1 card in hand
                if (candidate.IsAlive && candidate.RemainingCardCount > 0)
                {
                    return candidate;
                }
            }

            // No alive player has any cards remaining in this round
            return null;
        }

        /// <summary>
        /// Advances turn after a card play.
        /// </summary>
        public void AdvanceAfterPlay(GameMatch match, string currentPlayerId)
        {
            var nextPlayer = GetNextTurnPlayer(match, currentPlayerId);
            if (nextPlayer != null)
            {
                match.CurrentTurnPlayerId = nextPlayer.PlayerId;
                match.Phase = RoundPhase.AwaitPlayOrChallenge;
            }
            else
            {
                // All alive players are out of cards
                match.CurrentTurnPlayerId = null;
            }
        }
    }
}
