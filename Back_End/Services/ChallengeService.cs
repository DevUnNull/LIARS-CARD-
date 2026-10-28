using System.Security.Cryptography;
using System.Text;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;

namespace Back_End.Services
{
    public class ChallengeService
    {
        private readonly IMatchStore _matchStore;
        private readonly DeckRuleEngine _ruleEngine;
        private readonly PenaltyService _penaltyService;
        private readonly TableService? _tableService;
        private readonly GameBroadcastService? _gameBroadcastService;

        public ChallengeService(
            IMatchStore matchStore,
            DeckRuleEngine ruleEngine,
            PenaltyService penaltyService,
            TableService? tableService = null,
            GameBroadcastService? gameBroadcastService = null)
        {
            _matchStore = matchStore;
            _ruleEngine = ruleEngine;
            _penaltyService = penaltyService;
            _tableService = tableService;
            _gameBroadcastService = gameBroadcastService;
        }

        public async Task<ChallengeResponse> ChallengeAsync(string matchId, string playerId, ChallengeRequest request)
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

                // Compute payload hash for idempotency check
                string payloadString = $"{request.ActionId}_{request.ExpectedVersion}_{request.TargetPlayId}";
                string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadString)));

                var existingAction = _matchStore.GetActionResult(matchId, request.ActionId);
                if (existingAction != null)
                {
                    if (existingAction.PayloadHash == payloadHash && existingAction.Result is ChallengeResponse cachedResponse)
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

                // Validate challenge rules
                _ruleEngine.ValidateChallenge(match, playerId, request.TargetPlayId);

                // LastPlay is guaranteed non-null and unresolved by validation
                var lastPlay = match.LastPlay!;
                bool isBluff = _ruleEngine.IsBluff(lastPlay.Cards, match.TableRank);

                lastPlay.Resolved = true;
                lastPlay.WasBluff = isBluff;

                bool challengeSucceeded;
                string penalizedPlayerId;

                if (isBluff)
                {
                    // Challenger was right -> player who bluffed is penalized
                    challengeSucceeded = true;
                    penalizedPlayerId = lastPlay.PlayerId;
                }
                else
                {
                    // Challenger was wrong -> challenger is penalized
                    challengeSucceeded = false;
                    penalizedPlayerId = playerId;
                }

                // Chuyen match sang trang thai PenaltyResolution de cho nguoi thua chu dong bop co (Russian Roulette)
                match.Phase = RoundPhase.PenaltyResolution;
                match.PenalizedPlayerId = penalizedPlayerId;
                match.CurrentTurnPlayerId = penalizedPlayerId;
                match.StateVersion++;

                // Broadcast âm thanh Tố ("To") đồng bộ qua mạng tại vị trí người tố
                _gameBroadcastService?.BroadcastAudio(match, "To", playerId);

                // Broadcast sự kiện phạt tới tất cả Client trong phòng (kích hoạt UI bóp cò cho loser, spectating cho người khác)
                _gameBroadcastService?.BroadcastPenaltyWaitingForTrigger(match, penalizedPlayerId, playerId, challengeSucceeded);

                var response = new ChallengeResponse
                {
                    ChallengeSucceeded = challengeSucceeded,
                    PenalizedPlayerId = penalizedPlayerId,
                    Survived = false,
                    Eliminated = false,
                    NextPhase = "PenaltyResolution",
                    AwaitingTriggerPull = true,
                    StateVersion = match.StateVersion
                };

                // Persist state & action history
                _matchStore.SaveMatch(match);
                _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);

                return response;
            }
        }
    }
}
