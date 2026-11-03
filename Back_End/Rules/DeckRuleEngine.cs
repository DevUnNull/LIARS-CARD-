using Back_End.Exceptions;
using Back_End.Models;

namespace Back_End.Rules
{
    public class DeckRuleEngine
    {
        public void ValidatePlay(GameMatch match, string playerId, List<string> cardInstanceIds)
        {
            if (match.Phase != RoundPhase.AwaitPlayOrChallenge && match.Phase != RoundPhase.RoundStart)
            {
                throw GameRuleException.InvalidPhase("Trận đấu đang không ở phase cho phép đánh bài.", match.StateVersion);
            }

            if (match.CurrentTurnPlayerId != playerId)
            {
                throw GameRuleException.NotYourTurn(match.StateVersion);
            }

            var player = match.FindPlayer(playerId);
            if (player == null || !player.IsAlive)
            {
                throw GameRuleException.NotYourTurn(match.StateVersion);
            }

            if (cardInstanceIds == null || cardInstanceIds.Count < 1 || cardInstanceIds.Count > 3)
            {
                throw GameRuleException.InvalidCardCount(match.StateVersion);
            }

            // Must have unique card IDs in request
            if (cardInstanceIds.Distinct().Count() != cardInstanceIds.Count)
            {
                throw GameRuleException.InvalidCardCount(match.StateVersion);
            }

            // Verify player owns all cards
            var handCardIds = new HashSet<string>(player.Hand.Select(c => c.CardInstanceId));
            foreach (var cardId in cardInstanceIds)
            {
                if (!handCardIds.Contains(cardId))
                {
                    throw GameRuleException.CardNotOwned(match.StateVersion);
                }
            }

            // Note: Bluff is completely valid here, do NOT check card ranks against TableRank!
        }

        public void ValidateChallenge(GameMatch match, string playerId, string targetPlayId)
        {
            if (match.Phase != RoundPhase.AwaitPlayOrChallenge)
            {
                throw GameRuleException.InvalidPhase("Trận đấu đang không ở phase cho phép tố cáo.", match.StateVersion);
            }

            if (match.CurrentTurnPlayerId != playerId)
            {
                throw GameRuleException.NotYourTurn(match.StateVersion);
            }

            var challenger = match.FindPlayer(playerId);
            if (challenger == null || !challenger.IsAlive)
            {
                throw GameRuleException.NotYourTurn(match.StateVersion);
            }

            if (match.LastPlay == null)
            {
                throw GameRuleException.CannotChallenge("Chưa có lượt đánh nào để tố cáo.", match.StateVersion);
            }

            if (match.LastPlay.Resolved)
            {
                throw GameRuleException.AlreadyResolved(match.StateVersion);
            }

            if (!string.Equals(match.LastPlay.PlayId, targetPlayId, StringComparison.OrdinalIgnoreCase))
            {
                throw GameRuleException.CannotChallenge("TargetPlayId không khớp với lượt đánh hiện tại.", match.StateVersion);
            }
        }

        /// <summary>
        /// Determines whether the played cards constitute a bluff.
        /// Returns true if player bluffed (at least one card does not match tableRank and is not Joker).
        /// Returns false if all cards are genuine or Jokers.
        /// </summary>
        public bool IsBluff(List<Card> cards, string tableRank)
        {
            if (cards == null || cards.Count == 0)
            {
                return false;
            }

            foreach (var card in cards)
            {
                if (!card.MatchesRank(tableRank))
                {
                    return true; // Found a bluff card
                }
            }

            return false;
        }
    }
}
