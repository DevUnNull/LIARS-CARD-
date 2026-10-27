using Back_End.Exceptions;
using Back_End.Models;
using Back_End.Rules;

namespace Back_End.Services
{
    public class PenaltyResult
    {
        public bool ChallengeSucceeded { get; set; }
        public string PenalizedPlayerId { get; set; } = string.Empty;
        public bool Survived { get; set; }
        public bool Eliminated { get; set; }
        public string NextPhase { get; set; } = string.Empty;
    }

    public class PenaltyService
    {
        private readonly DeckRuleEngine _ruleEngine;

        public PenaltyService(DeckRuleEngine ruleEngine)
        {
            _ruleEngine = ruleEngine;
        }

        /// <summary>
        /// Validates and resolves a challenge and its server-side penalty.
        /// </summary>
        public PenaltyResult ApplyPenalty(GameMatch match, string challengerPlayerId)
        {
            if (match.Phase != RoundPhase.AwaitPlayOrChallenge)
            {
                throw GameRuleException.InvalidPhase("Không thể xử phạt ở phase hiện tại.", match.StateVersion);
            }

            var challenger = match.FindPlayer(challengerPlayerId);
            if (challenger == null || !challenger.IsAlive || match.CurrentTurnPlayerId != challengerPlayerId)
            {
                throw GameRuleException.NotYourTurn(match.StateVersion);
            }

            var lastPlay = match.LastPlay;
            if (lastPlay == null || lastPlay.Resolved)
            {
                throw GameRuleException.CannotChallenge("Không có lượt đánh hợp lệ để xử phạt.", match.StateVersion);
            }

            if (lastPlay.PlayerId == challengerPlayerId)
            {
                throw GameRuleException.CannotChallenge("Không thể tố cáo lượt đánh của chính mình.", match.StateVersion);
            }

            var playOwner = match.FindPlayer(lastPlay.PlayerId);
            if (playOwner == null || !playOwner.IsAlive ||
                lastPlay.Cards.Count is < 1 or > 3 ||
                lastPlay.Cards.Count != lastPlay.CardCount ||
                lastPlay.WasBluff.HasValue)
            {
                throw GameRuleException.CannotChallenge("Dữ liệu lượt đánh không hợp lệ để xử phạt.", match.StateVersion);
            }

            var isBluff = _ruleEngine.IsBluff(lastPlay.Cards, match.TableRank);
            var penalizedPlayerId = isBluff ? lastPlay.PlayerId : challengerPlayerId;
            var penalizedPlayer = match.FindPlayer(penalizedPlayerId);
            if (penalizedPlayer == null || !penalizedPlayer.IsAlive)
            {
                throw GameRuleException.CannotChallenge("Người chơi bị xử phạt không còn hợp lệ.", match.StateVersion);
            }

            penalizedPlayer.Revolver.ValidateState();

            lastPlay.Resolved = true;
            lastPlay.WasBluff = isBluff;
            match.Phase = RoundPhase.PenaltyResolution;

            bool isBulletHit = penalizedPlayer.Revolver.PullTrigger();
            bool survived = !isBulletHit;
            bool eliminated = isBulletHit;

            if (eliminated)
            {
                penalizedPlayer.IsAlive = false;
                penalizedPlayer.Hand.Clear();
            }

            // Check alive players
            var alivePlayers = match.AlivePlayers;
            string nextPhase;

            if (alivePlayers.Count <= 1)
            {
                // Match ends
                match.Phase = RoundPhase.MatchEnded;
                match.WinnerPlayerId = alivePlayers.FirstOrDefault()?.PlayerId;
                match.CurrentTurnPlayerId = null;
                match.StateVersion++;
                nextPhase = "MatchEnded";
            }
            else
            {
                // The caller orchestrates the next round after this penalty result is finalized.
                nextPhase = RoundPhase.PenaltyResolution.ToString();
            }

            return new PenaltyResult
            {
                ChallengeSucceeded = isBluff,
                PenalizedPlayerId = penalizedPlayerId,
                Survived = survived,
                Eliminated = eliminated,
                NextPhase = nextPhase
            };
        }
    }
}
