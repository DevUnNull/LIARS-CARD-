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
        public string? PenaltyEventId { get; set; }
    }

    public class PenaltyService
    {
        private readonly IMatchStore? _matchStore;
        private readonly DeckRoundService _deckRoundService;
        private readonly TableService? _tableService;
        private readonly GameBroadcastService? _gameBroadcastService;
        private readonly MatchResultService? _matchResultService;
        public int DefaultTimeoutMilliseconds { get; set; } = 10000;

        public PenaltyService(
            DeckRoundService deckRoundService,
            IMatchStore? matchStore = null,
            TableService? tableService = null,
            GameBroadcastService? gameBroadcastService = null,
            MatchResultService? matchResultService = null)
        {
            _deckRoundService = deckRoundService;
            _matchStore = matchStore;
            _tableService = tableService;
            _gameBroadcastService = gameBroadcastService;
            _matchResultService = matchResultService;
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

                // Check version: Cho phép sai lệch 1 version do Challenge vừa tăng version ngay trước đó, hoặc client gửi version <= 0
                if (request.ExpectedVersion > 0 && Math.Abs(match.StateVersion - request.ExpectedVersion) > 1)
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

                int shotNumber = player.Revolver.LastShotNumber;

                if (eliminated)
                {
                    player.IsAlive = false;
                    player.Hand.Clear(); // Xóa bài của người chơi đã chết
                    ResetRevolvers(match); // Trúng đạn: súng được nạp lại (đạn vị trí mới, đếm lại từ 1/6) cho round mới
                }

                var alivePlayers = match.AlivePlayers;
                string nextPhase = (alivePlayers.Count <= 1) ? "MatchEnded" : (survived ? "AwaitPlayOrChallenge" : "RoundStart");
                string? winnerPlayerId = (alivePlayers.Count <= 1) ? alivePlayers.FirstOrDefault()?.PlayerId : null;

                // Ghi nhận penalty event cho Animation ACK & Timeout
                string penaltyEventId = $"penalty_{match.MatchId}_{Guid.NewGuid().ToString("N")[..8]}";
                match.CurrentPenaltyEventId = penaltyEventId;
                match.PenaltyResolved = true;
                match.PenaltyResolvedAt = DateTime.UtcNow;
                match.PenaltyAckedPlayers.Clear();

                // Hoàn tất hình phạt và chuyển giao vòng đấu hoặc kết thúc trận ngay lập tức.
                // Tránh tình trạng match bị giữ ở PenaltyResolution khiến Client polling lặp lại màn hình bắn súng.
                await CompletePenaltyAndTransitionAsync(match);

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
                    winnerPlayerId,
                    shotNumber);

                var response = new PullTriggerResponse
                {
                    MatchId = matchId,
                    PenalizedPlayerId = playerId,
                    IsBulletHit = isBulletHit,
                    Survived = survived,
                    Eliminated = eliminated,
                    NextPhase = nextPhase,
                    WinnerPlayerId = winnerPlayerId,
                    StateVersion = match.StateVersion,
                    PenaltyEventId = penaltyEventId,
                    ShotNumber = shotNumber
                };

                // Persist state & action history
                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);

                // Khởi động Fallback Timeout: Nếu client không ACK sau một khoảng thời gian, server tự động chuyển vòng
                ScheduleTimeoutFallback(matchId, penaltyEventId, DefaultTimeoutMilliseconds);

                return response;
            }
        }

        /// <summary>Nạp lại súng của mọi người còn sống: đạn ở vị trí ngẫu nhiên mới, phát bắn đếm lại từ 1.</summary>
        private static void ResetRevolvers(GameMatch match)
        {
            foreach (var p in match.AlivePlayers)
                p.Revolver = new PlayerRevolver();
        }

        private void ScheduleTimeoutFallback(string matchId, string penaltyEventId, int timeoutMs)
        {
            if (timeoutMs <= 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(timeoutMs);
                    await ProcessPenaltyTimeoutAsync(matchId, penaltyEventId);
                }
                catch
                {
                    // Tránh unhandled task exception
                }
            });
        }

        /// <summary>
        /// Xử lý timeout khi client không gửi ACK. Server tự động giải tỏa trạng thái trận đấu.
        /// An toàn đa luồng, chống chuyển vòng 2 lần.
        /// </summary>
        public async Task<bool> ProcessPenaltyTimeoutAsync(string matchId, string penaltyEventId)
        {
            if (_matchStore == null) return false;

            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId);
                if (match == null) return false;

                // Chỉ thực hiện nếu đúng penalty event
                if (match.CurrentPenaltyEventId != penaltyEventId)
                {
                    return false;
                }

                // Nếu đã được chuyển vòng trước đó, xác nhận an toàn không chuyển lần hai
                if (match.RoundTransitionHandled)
                {
                    return true;
                }

                await CompletePenaltyAndTransitionAsync(match);
                _matchStore.SaveMatch(match);
                return true;
            }
        }

        /// <summary>
        /// Hoàn tất hình phạt và chuyển giao vòng đấu hoặc kết thúc trận (Dùng chung cho ACK và Timeout).
        /// </summary>
        public async Task CompletePenaltyAndTransitionAsync(GameMatch match)
        {
            if (match.RoundTransitionHandled)
            {
                return;
            }

            match.RoundTransitionHandled = true;
            var alivePlayers = match.AlivePlayers;

            if (alivePlayers.Count <= 1)
            {
                // Trận đấu kết thúc
                match.Phase = RoundPhase.MatchEnded;
                match.WinnerPlayerId = alivePlayers.FirstOrDefault()?.PlayerId;
                match.CurrentTurnPlayerId = null;
                match.PenalizedPlayerId = null;
                match.EndedAt = DateTime.UtcNow;
                match.StateVersion++;

                // Giải phóng bàn đấu
                _tableService?.EndMatchByMatchId(match.MatchId);

                // Lưu kết quả trận đấu bền vững vào MongoDB
                if (_matchResultService != null)
                {
                    await _matchResultService.SaveMatchResultAsync(match);
                }
            }
            else
            {
                // Vẫn còn ít nhất 2 người sống sót.
                // Bắn trượt (đã bóp cò, người đó còn sống): giữ nguyên bài, chỉ chuyển lượt.
                // Bắn trúng (có người bị loại): chia lại bài hoàn toàn mới.
                var shooter = match.PenalizedPlayerId != null ? match.FindPlayer(match.PenalizedPlayerId) : null;
                if (match.PenaltyResolved && shooter != null && shooter.IsAlive)
                {
                    _deckRoundService.ContinueRoundAfterMiss(match, shooter.PlayerId);
                }
                else
                {
                    match.PenalizedPlayerId = null;
                    _deckRoundService.StartRound(match);
                }
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

            int shotNumber = player.Revolver.LastShotNumber;

            if (eliminated)
            {
                player.IsAlive = false;
                player.Hand.Clear(); // Clear dead player hand
                ResetRevolvers(match);
            }

            // Check alive players
            var alivePlayers = match.AlivePlayers;
            string nextPhase;
            string penaltyEventId = $"penalty_{match.MatchId}_{Guid.NewGuid().ToString("N")[..8]}";
            match.CurrentPenaltyEventId = penaltyEventId;
            match.PenaltyResolved = true;
            match.PenaltyResolvedAt = DateTime.UtcNow;

            if (alivePlayers.Count <= 1)
            {
                // Match ends
                match.Phase = RoundPhase.MatchEnded;
                match.WinnerPlayerId = alivePlayers.FirstOrDefault()?.PlayerId;
                match.CurrentTurnPlayerId = null;
                match.PenalizedPlayerId = null;
                match.EndedAt = DateTime.UtcNow;
                match.RoundTransitionHandled = true;
                match.StateVersion++;
                nextPhase = "MatchEnded";
                _tableService?.EndMatchByMatchId(match.MatchId);

                if (_matchResultService != null)
                {
                    _ = _matchResultService.SaveMatchResultAsync(match);
                }
            }
            else
            {
                match.RoundTransitionHandled = true;
                if (survived)
                {
                    // Bắn trượt: giữ nguyên bài, chuyển lượt
                    _deckRoundService.ContinueRoundAfterMiss(match, penalizedPlayerId);
                    nextPhase = "AwaitPlayOrChallenge";
                }
                else
                {
                    // Bắn trúng: chia lại bài mới cho người còn sống
                    match.PenalizedPlayerId = null;
                    _deckRoundService.StartRound(match);
                    nextPhase = "RoundStart";
                }
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
                match.WinnerPlayerId,
                shotNumber);

            return new PenaltyResult
            {
                Survived = survived,
                Eliminated = eliminated,
                NextPhase = nextPhase,
                PenaltyEventId = penaltyEventId
            };
        }
    }
}
