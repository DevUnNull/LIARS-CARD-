using System.Security.Cryptography;
using System.Text;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
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
        private readonly IMatchStore? _matchStore;
        private readonly DeckRoundService _deckRoundService;
        private readonly TableService? _tableService;
        private readonly GameBroadcastService? _gameBroadcastService;

        public PenaltyService(
            DeckRoundService deckRoundService,
            IMatchStore? matchStore = null,
            TableService? tableService = null,
            GameBroadcastService? gameBroadcastService = null)
        {
            _deckRoundService = deckRoundService;
            _matchStore = matchStore;
            _tableService = tableService;
            _gameBroadcastService = gameBroadcastService;
        }

        /// <summary>
        /// Xử lý bóp cò súng Nga (Russian Roulette) do người chơi bị phạt CHỦ ĐỘNG bấm giữ chuột để bóp cò.
        /// </summary>
        public async Task<PullTriggerResponse> PullTriggerAsync(string matchId, string playerId, PullTriggerRequest request)
        {
            if (_matchStore == null)
            {
                throw new InvalidOperationException("MatchStore is not configured in PenaltyService.");
            }

            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId);
                if (match == null)
                {
                    throw GameRuleException.MatchNotFound();
                }

                var player = match.FindPlayer(playerId);
                if (player == null)
                {
                    throw GameRuleException.NotMatchMember();
                }

                // Idempotency check
                string payloadString = $"{request.ActionId}_{request.ExpectedVersion}";
                string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadString)));

                var existingAction = _matchStore.GetActionResult(matchId, request.ActionId);
                if (existingAction != null)
                {
                    if (existingAction.PayloadHash == payloadHash && existingAction.Result is PullTriggerResponse cachedResponse)
                    {
                        return cachedResponse;
                    }

                    throw GameRuleException.DuplicateAction(match.StateVersion);
                }

                // Check version
                if (match.StateVersion != request.ExpectedVersion)
                {
                    throw GameRuleException.StaleVersion(match.StateVersion);
                }

                // Validate game phase
                if (match.Phase != RoundPhase.PenaltyResolution)
                {
                    throw new GameRuleException("INVALID_PHASE", "Trận đấu hiện không ở trong trạng thái xử phạt bóp cò súng!");
                }

                // Validate that ONLY the penalized loser player can pull trigger
                if (!string.Equals(match.PenalizedPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new GameRuleException("NOT_PENALIZED_PLAYER", "Chỉ người chơi thua cuộc bị phạt mới có quyền tự bóp cò súng!");
                }

                // Thực hiện tính toán RNG: buồng đạn nổ hay rỗng
                bool isBulletHit = player.Revolver.PullTrigger();
                bool survived = !isBulletHit;
                bool eliminated = isBulletHit;

                if (eliminated)
                {
                    player.IsAlive = false;
                    player.Hand.Clear(); // Xóa bài của người chơi đã chết
                }

                var alivePlayers = match.AlivePlayers;
                string nextPhase;
                string? winnerPlayerId = null;

                if (alivePlayers.Count <= 1)
                {
                    match.Phase = RoundPhase.MatchEnded;
                    match.WinnerPlayerId = alivePlayers.FirstOrDefault()?.PlayerId;
                    match.CurrentTurnPlayerId = null;
                    match.PenalizedPlayerId = null;
                    match.StateVersion++;
                    nextPhase = "MatchEnded";
                    winnerPlayerId = match.WinnerPlayerId;

                    // Giải phóng bàn đấu về trạng thái Waiting
                    _tableService?.EndMatchByMatchId(matchId);
                }
                else
                {
                    match.PenalizedPlayerId = null;
                    // Bắt đầu vòng đấu mới cho những người chơi còn sống
                    _deckRoundService.StartRound(match);
                    nextPhase = "RoundStart";
                }

                // Broadcast âm thanh phát súng: Nổ lớn (Gunshot) hoặc Cò gõ rỗng (DryFire)
                string soundName = isBulletHit ? "Gunshot" : "DryFire";
                _gameBroadcastService?.BroadcastAudio(match, soundName, playerId);

                // Broadcast kết quả bắn súng cho toàn bộ Client trên bàn đấu
                _gameBroadcastService?.BroadcastTriggerResult(
                    match,
                    playerId,
                    isBulletHit,
                    survived,
                    eliminated,
                    nextPhase,
                    winnerPlayerId);

                var response = new PullTriggerResponse
                {
                    MatchId = matchId,
                    PenalizedPlayerId = playerId,
                    IsBulletHit = isBulletHit,
                    Survived = survived,
                    Eliminated = eliminated,
                    NextPhase = nextPhase,
                    WinnerPlayerId = winnerPlayerId,
                    StateVersion = match.StateVersion
                };

                // Persist state & action history
                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);

                return response;
            }
        }

        /// <summary>
        /// Executes Russian Roulette penalty synchronously on the penalized player.
        /// Maintained for unit testing and backwards-compatibility.
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
                match.PenalizedPlayerId = null;
                match.StateVersion++;
                nextPhase = "MatchEnded";
                _tableService?.EndMatchByMatchId(match.MatchId);
            }
            else
            {
                match.PenalizedPlayerId = null;
                // Start next round with living players
                _deckRoundService.StartRound(match);
                nextPhase = "RoundStart";
            }

            string soundName = isBulletHit ? "Gunshot" : "DryFire";
            _gameBroadcastService?.BroadcastAudio(match, soundName, penalizedPlayerId);

            _gameBroadcastService?.BroadcastTriggerResult(
                match,
                penalizedPlayerId,
                isBulletHit,
                survived,
                eliminated,
                nextPhase,
                match.WinnerPlayerId);

            return new PenaltyResult
            {
                Survived = survived,
                Eliminated = eliminated,
                NextPhase = nextPhase
            };
        }
    }
}
