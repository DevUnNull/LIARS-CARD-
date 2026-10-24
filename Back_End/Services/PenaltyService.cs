using Back_End.Exceptions;
using Back_End.Models;

namespace Back_End.Services
{
    public class PenaltyResult
    {
        public bool Survived { get; set; }
        public bool Eliminated { get; set; }
        public string NextPhase { get; set; } = string.Empty;
    }

    public class PenaltyService
    {
        private readonly DeckRoundService _deckRoundService;

        public PenaltyService(DeckRoundService deckRoundService)
        {
            _deckRoundService = deckRoundService;
        }

        /// <summary>
        /// Executes Russian Roulette penalty on the penalized player.
        /// Resolves survival/elimination, checks remaining players,
        /// and either starts a new round or marks match as ended.
        /// </summary>
        public PenaltyResult ApplyPenalty(GameMatch match, string penalizedPlayerId)
        {
            var player = match.FindPlayer(penalizedPlayerId);
            if (player == null)
            {
                throw new InvalidOperationException($"Không tìm thấy người chơi bị xử phạt: {penalizedPlayerId}");
            }

            match.Phase = RoundPhase.PenaltyResolution;

            // Pull trigger on revolver
            bool isBulletHit = player.Revolver.PullTrigger();
            bool survived = !isBulletHit;
            bool eliminated = isBulletHit;

            if (eliminated)
            {
                player.IsAlive = false;
                player.Hand.Clear(); // Clear dead player hand
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
                // Start next round with living players
                _deckRoundService.StartRound(match);
                nextPhase = "RoundStart";
            }

            return new PenaltyResult
            {
                Survived = survived,
                Eliminated = eliminated,
                NextPhase = nextPhase
            };
        }
    }
}
