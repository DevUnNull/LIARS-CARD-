using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;

namespace Back_End.Services
{
    public class MatchQueryService
    {
        private readonly IMatchStore _matchStore;

        public MatchQueryService(IMatchStore matchStore)
        {
            _matchStore = matchStore;
        }

        public PublicMatchStateResponse GetPublicState(string matchId)
        {
            var match = _matchStore.GetMatch(matchId);
            if (match == null)
            {
                throw GameRuleException.MatchNotFound();
            }

            var response = new PublicMatchStateResponse
            {
                MatchId = match.MatchId,
                RoundNumber = match.RoundNumber,
                TableRank = match.TableRank,
                CurrentTurnPlayerId = match.CurrentTurnPlayerId,
                Phase = match.Phase.ToString(),
                StateVersion = match.StateVersion,
                WinnerPlayerId = match.WinnerPlayerId,
                PenalizedPlayerId = (match.Phase == RoundPhase.PenaltyResolution && !match.PenaltyResolved) ? match.PenalizedPlayerId : null,
                CurrentPenaltyEventId = match.CurrentPenaltyEventId,
                Players = match.Players.Select(p => new PublicPlayerStateDto
                {
                    PlayerId = p.PlayerId,
                    IsAlive = p.IsAlive,
                    RemainingCardCount = p.RemainingCardCount,
                    IsConnected = p.IsConnected
                }).ToList()
            };

            if (match.LastPlay != null)
            {
                response.LastPlay = new PublicLastPlayDto
                {
                    PlayId = match.LastPlay.PlayId,
                    PlayerId = match.LastPlay.PlayerId,
                    CardCount = match.LastPlay.CardCount
                };
            }

            return response;
        }

        public PlayerHandResponse GetPlayerHand(string matchId, string playerId)
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

            return new PlayerHandResponse
            {
                MatchId = match.MatchId,
                Cards = player.Hand.Select(c => new CardDto
                {
                    CardInstanceId = c.CardInstanceId,
                    Rank = c.Rank
                }).ToList()
            };
        }
    }
}
