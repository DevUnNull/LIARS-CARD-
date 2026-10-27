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
        private readonly DeckRoundService _deckRoundService;
        private readonly IPenaltyAnimationService _penaltyAnimationService;
        private readonly IGameplayStateNotifier _gameplayStateNotifier;
        private readonly IMatchResultService _matchResultService;

        public ChallengeService(
            IMatchStore matchStore,
            DeckRuleEngine ruleEngine,
            PenaltyService penaltyService,
            DeckRoundService deckRoundService,
            IPenaltyAnimationService penaltyAnimationService,
            IGameplayStateNotifier gameplayStateNotifier,
            IMatchResultService matchResultService)
        {
            _matchStore = matchStore;
            _ruleEngine = ruleEngine;
            _penaltyService = penaltyService;
            _deckRoundService = deckRoundService;
            _penaltyAnimationService = penaltyAnimationService;
            _gameplayStateNotifier = gameplayStateNotifier;
            _matchResultService = matchResultService;
        }

        public async Task<ChallengeResponse> ChallengeAsync(string matchId, string playerId, ChallengeRequest request)
        {
            ChallengeResponse response;
            PenaltyAnimationSession? animationSession = null;
            PlayerEliminatedEvent? eliminatedEvent = null;
            MatchEndedEvent? matchEndedEvent = null;
            RoundStartedEvent? roundStartedEvent = null;
            bool isCachedActionRetry = false;

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
                        if (match.Phase != RoundPhase.MatchEnded)
                        {
                            return cachedResponse;
                        }

                        response = cachedResponse;
                        isCachedActionRetry = true;
                        matchEndedEvent = new MatchEndedEvent(
                            match.MatchId,
                            match.StateVersion,
                            match.WinnerPlayerId);
                    }
                    else
                    {
                        throw GameRuleException.DuplicateAction(match.StateVersion);
                    }
                }
                else
                {
                    // Check version
                    if (match.StateVersion != request.ExpectedVersion)
                    {
                        throw GameRuleException.StaleVersion(match.StateVersion);
                    }

                    // Validate challenge rules
                    _ruleEngine.ValidateChallenge(match, playerId, request.TargetPlayId);

                    var penaltyResult = _penaltyService.ApplyPenalty(match, playerId);
                    var penaltyEventId = Guid.NewGuid();

                    if (match.Phase != RoundPhase.MatchEnded &&
                        match.WinnerPlayerId == null &&
                        match.AlivePlayers.Count > 1)
                    {
                        _deckRoundService.StartRound(match);
                        roundStartedEvent = new RoundStartedEvent(
                            match.MatchId,
                            penaltyEventId,
                            match.RoundNumber,
                            match.StateVersion,
                            match.Phase.ToString(),
                            match.TableRank,
                            match.CurrentTurnPlayerId);
                        penaltyResult.NextPhase = match.Phase.ToString();
                    }

                    response = new ChallengeResponse
                    {
                        ChallengeSucceeded = penaltyResult.ChallengeSucceeded,
                        PenalizedPlayerId = penaltyResult.PenalizedPlayerId,
                        Survived = penaltyResult.Survived,
                        Eliminated = penaltyResult.Eliminated,
                        NextPhase = penaltyResult.NextPhase,
                        StateVersion = match.StateVersion,
                        PenaltyEventId = penaltyEventId
                    };

                    animationSession = _penaltyAnimationService.CreateSession(
                        match,
                        penaltyEventId,
                        penaltyResult);

                    if (penaltyResult.Eliminated)
                    {
                        eliminatedEvent = new PlayerEliminatedEvent(
                            match.MatchId,
                            penaltyResult.PenalizedPlayerId,
                            match.StateVersion);
                    }

                    if (match.Phase == RoundPhase.MatchEnded)
                    {
                        matchEndedEvent = new MatchEndedEvent(
                            match.MatchId,
                            match.StateVersion,
                            match.WinnerPlayerId);
                    }

                    _matchStore.SaveMatch(match);
                    _matchStore.SaveActionResult(matchId, request.ActionId, payloadHash, response, match.StateVersion);
                }
            }

            if (matchEndedEvent != null)
            {
                var persistedResult = await _matchResultService.PersistCompletedMatchAsync(
                    matchId,
                    CancellationToken.None,
                    isCachedActionRetry);
                if (persistedResult.FinalStateVersion != matchEndedEvent.StateVersion ||
                    persistedResult.WinnerPlayerId != matchEndedEvent.WinnerPlayerId)
                {
                    throw new MatchResultConflictException(
                        "The match state changed while its final result was being persisted.");
                }
            }

            if (eliminatedEvent != null)
            {
                await _gameplayStateNotifier.PublishPlayerEliminatedAsync(
                    eliminatedEvent,
                    CancellationToken.None);
            }

            if (matchEndedEvent != null &&
                _matchStore.TryMarkMatchEndedEventPublished(matchId, matchEndedEvent.StateVersion))
            {
                await _gameplayStateNotifier.PublishMatchEndedAsync(
                    matchEndedEvent,
                    CancellationToken.None);
            }

            if (roundStartedEvent != null)
            {
                await _gameplayStateNotifier.PublishRoundStartedAsync(
                    roundStartedEvent,
                    CancellationToken.None);
            }

            if (animationSession != null)
            {
                await _penaltyAnimationService.PublishStartedAsync(
                    animationSession,
                    CancellationToken.None);
            }

            return response;
        }
    }
}
