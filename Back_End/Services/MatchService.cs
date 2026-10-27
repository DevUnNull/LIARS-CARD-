using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;

namespace Back_End.Services
{
    public class MatchService
    {
        private readonly IMatchStore _matchStore;
        private readonly DeckRoundService _deckRoundService;

        public MatchService(IMatchStore matchStore, DeckRoundService deckRoundService)
        {
            _matchStore = matchStore;
            _deckRoundService = deckRoundService;
        }

        public GameMatch CreateMatch(string? matchId, List<string> playerIds)
        {
            string id = string.IsNullOrWhiteSpace(matchId) ? $"match_{Guid.NewGuid():N}" : matchId;

            var match = new GameMatch
            {
                MatchId = id,
                Phase = RoundPhase.WaitingForPlayers,
                StateVersion = 1,
                RoundNumber = 0
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
                    IsReady = false
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

                if (allReady && match.Phase == RoundPhase.WaitingForPlayers)
                {
                    _deckRoundService.StartRound(match);
                }

                _matchStore.SaveMatch(match);
                return match;
            }
        }
    }
}
