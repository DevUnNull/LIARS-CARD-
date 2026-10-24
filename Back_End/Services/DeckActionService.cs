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

        public DeckActionService(
            IMatchStore matchStore,
            DeckRuleEngine ruleEngine,
            TurnStateMachine turnStateMachine)
        {
            _matchStore = matchStore;
            _ruleEngine = ruleEngine;
            _turnStateMachine = turnStateMachine;
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
    }
}
