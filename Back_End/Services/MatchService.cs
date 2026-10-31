using System.Security.Cryptography;
using System.Text;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;

namespace Back_End.Services
{
    public class MatchService
    {
        private readonly IMatchStore _matchStore;
        private readonly DeckRoundService _deckRoundService;
        private readonly PenaltyService? _penaltyService;
        private readonly ConnectionTicketService? _ticketService;
        private readonly MatchResultService? _matchResultService;

        public MatchService(
            IMatchStore matchStore,
            DeckRoundService deckRoundService,
            PenaltyService? penaltyService = null,
            ConnectionTicketService? ticketService = null,
            MatchResultService? matchResultService = null)
        {
            _matchStore = matchStore;
            _deckRoundService = deckRoundService;
            _penaltyService = penaltyService;
            _ticketService = ticketService;
            _matchResultService = matchResultService;
        }

        public GameMatch CreateMatch(string? matchId, List<string> playerIds)
        {
            string id = string.IsNullOrWhiteSpace(matchId) ? $"match_{Guid.NewGuid():N}" : matchId;

            var match = new GameMatch
            {
                MatchId = id,
                Phase = RoundPhase.WaitingForPlayers,
                StateVersion = 1,
                RoundNumber = 0,
                StartedAt = DateTime.UtcNow
            };

            int seat = 0;
            foreach (var pid in playerIds.Distinct())
            {
                match.Players.Add(new MatchPlayer
                {
                    PlayerId = pid,
                    DisplayName = pid,
                    SeatIndex = seat++,
                    IsAlive = true,
                    IsReady = false,
                    IsConnected = false
                });
            }

            _matchStore.SaveMatch(match);
            return match;
        }

        public async Task<GameMatch> SetReadyAsync(string matchId, string playerId, bool isReady)
        {
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

                player.IsReady = isReady;

                // Check if all living players are ready (requires at least 2 players)
                var alivePlayers = match.AlivePlayers;
                bool allReady = alivePlayers.Count >= 2 && alivePlayers.All(p => p.IsReady);

                if (allReady && (match.Phase == RoundPhase.WaitingForPlayers || match.Phase == RoundPhase.MatchEnded))
                {
                    _deckRoundService.StartRound(match);
                }

                _matchStore.SaveMatch(match);
                return match;
            }
        }

        /// <summary>
        /// Xác nhận đã xem xong animation xử phạt (ACK).
        /// Nghiệp vụ server tiến hành chuyển vòng hoặc kết thúc trận một cách an toàn và idempotent.
        /// </summary>
        public async Task<PenaltyAckResponse> AcknowledgePenaltyAsync(string matchId, string playerId, PenaltyAckRequest request)
        {
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

                // Idempotency check theo actionId
                string payloadString = $"{request.ActionId}_{request.PenaltyEventId}";
                string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadString)));

                var existingAction = _matchStore.GetActionResult(matchId, request.ActionId);
                if (existingAction != null)
                {
                    if (existingAction.PayloadHash == payloadHash && existingAction.Result is PenaltyAckResponse cachedResponse)
                    {
                        return cachedResponse;
                    }

                    throw GameRuleException.DuplicateAction(match.StateVersion);
                }

                // Kiểm tra penaltyEventId
                if (!string.Equals(match.CurrentPenaltyEventId, request.PenaltyEventId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new GameRuleException("WRONG_PENALTY", "PenaltyEventId không hợp lệ hoặc đã hết hiệu lực.");
                }

                // Kiểm tra phase
                if (match.Phase != RoundPhase.PenaltyResolution)
                {
                    // Nếu match đã được chuyển vòng bởi một ACK trước đó hoặc Timeout
                    if (match.RoundTransitionHandled)
                    {
                        var idempotentResponse = new PenaltyAckResponse
                        {
                            MatchId = matchId,
                            PenaltyEventId = request.PenaltyEventId,
                            Accepted = true,
                            NextPhase = match.Phase.ToString(),
                            WinnerPlayerId = match.WinnerPlayerId,
                            StateVersion = match.StateVersion
                        };
                        _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, idempotentResponse, match.StateVersion);
                        return idempotentResponse;
                    }

                    throw new GameRuleException("INVALID_PHASE", "Trận đấu hiện không ở trong trạng thái xử phạt cần xác nhận animation.");
                }

                // Ghi nhận ACK từ người chơi này
                match.PenaltyAckedPlayers.Add(playerId);

                // Tiến hành điều phối chuyển vòng nếu chưa xử lý
                if (!match.RoundTransitionHandled && _penaltyService != null)
                {
                    await _penaltyService.CompletePenaltyAndTransitionAsync(match);
                }

                var response = new PenaltyAckResponse
                {
                    MatchId = matchId,
                    PenaltyEventId = request.PenaltyEventId,
                    Accepted = true,
                    NextPhase = match.Phase.ToString(),
                    WinnerPlayerId = match.WinnerPlayerId,
                    StateVersion = match.StateVersion
                };

                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);

                return response;
            }
        }

        /// <summary>
        /// Người chơi kết nối vào trận đấu bằng Join Ticket nhận được từ bàn chơi (Handoff).
        /// </summary>
        public async Task<GameMatch> JoinMatchAsync(string matchId, string playerId, string joinTicket)
        {
            if (_ticketService != null)
            {
                var (isValid, error) = _ticketService.ValidateAndConsume(joinTicket, playerId, matchId);
                if (!isValid)
                {
                    if (error == "TICKET_OWNER_MISMATCH")
                    {
                        throw new GameRuleException("TICKET_OWNER_MISMATCH", "Join ticket không thuộc về tài khoản của bạn.");
                    }
                    if (error == "TICKET_EXPIRED")
                    {
                        throw new GameRuleException("TICKET_EXPIRED", "Join ticket đã hết hạn.");
                    }
                    if (error == "TICKET_MATCH_MISMATCH")
                    {
                        throw new GameRuleException("TICKET_MATCH_MISMATCH", "Join ticket không dùng cho trận đấu này.");
                    }

                    throw new GameRuleException("INVALID_OR_USED_TICKET", "Join ticket không hợp lệ hoặc đã được sử dụng.");
                }
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

                player.IsConnected = true;
                match.StateVersion++;
                _matchStore.SaveMatch(match);
                return match;
            }
        }

        /// <summary>
        /// Loại người chơi khỏi trận đấu và xóa bài.
        /// </summary>
        public void EliminatePlayer(GameMatch match, string playerId)
        {
            var player = match.FindPlayer(playerId);
            if (player != null)
            {
                player.IsAlive = false;
                player.Hand.Clear();
            }
        }

        /// <summary>
        /// Kiểm tra điều kiện chiến thắng của trận đấu:
        /// - Còn >= 2 người: tiếp tục.
        /// - Còn 1 người: người đó là người chiến thắng duy nhất.
        /// </summary>
        public (bool hasWinner, string? winnerId) CheckWinner(GameMatch match)
        {
            var alivePlayers = match.AlivePlayers;
            if (alivePlayers.Count == 1)
            {
                return (true, alivePlayers[0].PlayerId);
            }

            if (alivePlayers.Count == 0)
            {
                return (true, null);
            }

            return (false, null);
        }

        /// <summary>
        /// Kết thúc trận đấu, gán người chiến thắng và chuyển phase sang MatchEnded.
        /// </summary>
        public async Task FinishMatchAsync(GameMatch match, string? winnerId)
        {
            if (match.Phase == RoundPhase.MatchEnded)
            {
                return;
            }

            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = winnerId;
            match.CurrentTurnPlayerId = null;
            match.PenalizedPlayerId = null;
            match.EndedAt = DateTime.UtcNow;
            match.StateVersion++;

            if (_matchResultService != null)
            {
                await _matchResultService.SaveMatchResultAsync(match);
            }

            _matchStore.SaveMatch(match);
        }
    }
}
