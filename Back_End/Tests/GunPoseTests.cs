using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Xunit;

namespace Back_End.Tests
{
    public class GunPoseTests
    {
        private static GameMatch NewMatch(string id, RoundPhase phase, params string[] players)
        {
            var match = new GameMatch { MatchId = id, Phase = phase };
            foreach (var p in players) match.Players.Add(new MatchPlayer { PlayerId = p });
            return match;
        }

        [Fact]
        public void FindActiveMatchByPlayer_ReturnsOnlyUnfinishedMatchOfThatPlayer()
        {
            var store = new InMemoryMatchStore();
            store.SaveMatch(NewMatch("old", RoundPhase.MatchEnded, "p1", "p2"));
            store.SaveMatch(NewMatch("live", RoundPhase.PenaltyResolution, "p1", "p3"));

            Assert.Equal("live", store.FindActiveMatchByPlayer("p1")?.MatchId);
            Assert.Null(store.FindActiveMatchByPlayer("p2"));   // chỉ ở trận đã kết thúc
            Assert.Null(store.FindActiveMatchByPlayer("nobody"));
        }
    }
}
