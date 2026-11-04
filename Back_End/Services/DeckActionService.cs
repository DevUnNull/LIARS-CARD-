using System.Security.Cryptography;
using System.Text;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;

namespace Back_End.Services
{
    public class DeckActionService
    {
        private readonly IMatchStore _matchStore;
        private readonly DeckRuleEngine _ruleEngine;
        private readonly TurnStateMachine _turnStateMachine;
        private readonly GameBroadcastService? _gameBroadcastService;
        private readonly DeckRoundService? _deckRoundService;

        public DeckActionService(
            IMatchStore matchStore,
            DeckRuleEngine ruleEngine,
            TurnStateMachine turnStateMachine,
            GameBroadcastService? gameBroadcastService = null,
            DeckRoundService? deckRoundService = null)
        {
            _deckRoundService = deckRoundService;
            _matchStore = matchStore;
            _ruleEngine = ruleEngine;
            _turnStateMachine = turnStateMachine;
            _gameBroadcastService = gameBroadcastService;
        }

        public async Task<PlayCardsResponse> PlayCardsAsync(string matchId, string playerId, PlayCardsRequest request)
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

                // Compute deterministic payload hash for idempotency check
                string sortedCards = string.Join(",", (request.CardInstanceIds ?? new List<string>()).OrderBy(c => c));
                string payloadString = $"{request.ActionId}_{request.ExpectedVersion}_{sortedCards}";
                string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadString)));

                var existingAction = _matchStore.GetActionResult(matchId, request.ActionId);
                if (existingAction != null)
                {
                    if (existingAction.PayloadHash == payloadHash && existingAction.Result is PlayCardsResponse cachedResponse)
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

                // Validate gameplay rules
                _ruleEngine.ValidatePlay(match, playerId, request.CardInstanceIds ?? new List<string>());

                // Extract played cards
                var playedCardIds = new HashSet<string>(request.CardInstanceIds ?? new List<string>());
                var playedCards = player.Hand.Where(c => playedCardIds.Contains(c.CardInstanceId)).ToList();

                // Remove cards from player's hand
                player.Hand.RemoveAll(c => playedCardIds.Contains(c.CardInstanceId));

                // Record LastPlay
                string playId = $"play_{Guid.NewGuid():N}";
                var lastPlay = new LastPlay
                {
                    PlayId = playId,
                    PlayerId = playerId,
                    CardCount = playedCards.Count,
                    Cards = playedCards,
                    PlayedAt = DateTime.UtcNow,
                    Resolved = false
                };
                match.LastPlay = lastPlay;

                // Advance turn to next eligible player
                _turnStateMachine.AdvanceAfterPlay(match, playerId);

                // Increment state version
                match.StateVersion++;

                // Broadcast âm thanh đánh bài 3D tương ứng với số lượng lá bài vừa đánh ra
                string cardSfx = playedCards.Count switch
                {
                    1 => "Danh_1",
                    2 => "Danh_2",
                    3 => "Dnah_3",
                    _ => "Danh_1"
                };
                _gameBroadcastService?.BroadcastAudio(match, cardSfx, playerId);

                var response = new PlayCardsResponse
                {
                    Accepted = true,
                    PlayId = playId,
                    PlayedCardCount = playedCards.Count,
                    RemainingCardCount = player.RemainingCardCount,
                    StateVersion = match.StateVersion
                };

                // Persist state & action history
                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);

                return response;
            }
        }

        /// <summary>
        /// "Tin": người đánh trước đã hết bài và người đến lượt không tố. Khi tất cả người còn bài đều Tin,
        /// người hết bài an toàn -> vòng kết thúc, chia lại bài mới (súng giữ nguyên). Chưa đủ thì chuyển hỏi người kế.
        /// </summary>
        public async Task<ChallengeResponse> TrustAsync(string matchId, string playerId, ChallengeRequest request)
        {
            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId) ?? throw GameRuleException.MatchNotFound();
                if (match.FindPlayer(playerId) == null) throw GameRuleException.NotMatchMember();

                string payloadHash = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes($"trust_{request.ActionId}_{request.ExpectedVersion}_{request.TargetPlayId}")));
                var existing = _matchStore.GetActionResult(matchId, request.ActionId);
                if (existing != null)
                {
                    if (existing.PayloadHash == payloadHash && existing.Result is ChallengeResponse cached) return cached;
                    throw GameRuleException.DuplicateAction(match.StateVersion);
                }

                if (match.StateVersion != request.ExpectedVersion) throw GameRuleException.StaleVersion(match.StateVersion);

                _ruleEngine.ValidateChallenge(match, playerId, request.TargetPlayId);
                if (!_ruleEngine.IsLastPlayOutOfCards(match))
                {
                    throw GameRuleException.CannotChallenge("Chỉ được Tin khi đối thủ đã đánh hết bài.", match.StateVersion);
                }

                match.TrustedPlayers.Add(playerId);
                var ownerId = match.LastPlay!.PlayerId;

                // Người còn phải quyết định: còn sống, còn bài, chưa Tin
                var pending = match.Players
                    .Where(p => p.IsAlive && p.RemainingCardCount > 0 && p.PlayerId != ownerId && !match.TrustedPlayers.Contains(p.PlayerId))
                    .ToList();

                string nextPhase;
                if (pending.Count == 0)
                {
                    // Không ai tố: người hết bài an toàn -> chia lại bài cho cả bàn
                    match.LastPlay!.Resolved = true;
                    if (_deckRoundService == null) throw new InvalidOperationException("DeckRoundService is not configured.");
                    _deckRoundService.StartRound(match);
                    nextPhase = match.Phase == RoundPhase.MatchEnded ? "MatchEnded" : "RoundStart";
                }
                else
                {
                    // Chuyển cho người kế tiếp theo chiều ghế trong số người chưa quyết định
                    int cur = match.Players.FindIndex(p => p.PlayerId == playerId);
                    var next = Enumerable.Range(1, match.Players.Count)
                        .Select(s => match.Players[(cur + s) % match.Players.Count])
                        .First(p => pending.Contains(p));
                    match.CurrentTurnPlayerId = next.PlayerId;
                    match.StateVersion++;
                    nextPhase = "AwaitPlayOrChallenge";
                }

                var response = new ChallengeResponse
                {
                    ChallengeSucceeded = false,
                    PenalizedPlayerId = string.Empty,
                    NextPhase = nextPhase,
                    AwaitingTriggerPull = false,
                    StateVersion = match.StateVersion
                };

                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);
                return response;
            }
        }
    }
}
