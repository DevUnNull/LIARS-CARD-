using System.Security.Claims;
using Back_End.Controllers;
using Back_End.Data;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;
using Back_End.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Back_End.Tests
{
    public class TuanModuleTests
    {
        private class TestHarness
        {
            public InMemoryMatchStore Store { get; set; } = new();
            public DeckRuleEngine RuleEngine { get; set; } = new();
            public TurnStateMachine TurnStateMachine { get; set; } = new();
            public DeckRoundService RoundService { get; set; } = null!;
            public MatchResultRepository ResultRepository { get; set; } = new();
            public MatchResultService ResultService { get; set; } = null!;
            public TableService TableService { get; set; } = new();
            public ConnectionTicketService TicketService { get; set; } = new();
            public PenaltyService PenaltyService { get; set; } = null!;
            public MatchService MatchService { get; set; } = null!;
            public DeckActionService ActionService { get; set; } = null!;
            public ChallengeService ChallengeService { get; set; } = null!;
            public MatchQueryService QueryService { get; set; } = null!;

            public static TestHarness Create()
            {
                var h = new TestHarness();
                h.RoundService = new DeckRoundService(h.TurnStateMachine);
                h.ResultService = new MatchResultService(h.ResultRepository, h.Store);
                h.PenaltyService = new PenaltyService(h.RoundService, h.Store, h.TableService, null, h.ResultService);
                h.PenaltyService.DefaultTimeoutMilliseconds = 0; // Disable auto background delay in tests
                h.MatchService = new MatchService(h.Store, h.RoundService, h.PenaltyService, h.TicketService, h.ResultService);
                h.ActionService = new DeckActionService(h.Store, h.RuleEngine, h.TurnStateMachine);
                h.ChallengeService = new ChallengeService(h.Store, h.RuleEngine, h.PenaltyService, h.TableService);
                h.QueryService = new MatchQueryService(h.Store);
                return h;
            }
        }

        private GameMatch CreateMatchWithPlayers(TestHarness h, int playerCount = 2, string fixedRank = "K")
        {
            var playerIds = Enumerable.Range(1, playerCount).Select(i => $"player_{i:D2}").ToList();
            var match = h.MatchService.CreateMatch("match_" + Guid.NewGuid().ToString("N")[..6], playerIds);

            foreach (var pid in playerIds)
            {
                var p = match.FindPlayer(pid)!;
                p.IsReady = true;
            }

            h.RoundService.StartRound(match, preferredStartingSeat: 0, fixedTableRank: fixedRank);
            h.Store.SaveMatch(match);
            return match;
        }

        // =========================================================================
        // GROUP 1: PENALTY (TEST CASES 1 - 4)
        // =========================================================================

        [Fact]
        public async Task TestCase01_PlayerHitsBullet_Eliminated()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            // Chamber 0 has bullet -> will hit bullet
            p1.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0);
            h.Store.SaveMatch(match);

            var res = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            Assert.True(res.IsBulletHit);
            Assert.True(res.Eliminated);
            Assert.False(res.Survived);
            Assert.False(p1.IsAlive);
            Assert.Empty(p1.Hand);
        }

        [Fact]
        public async Task TestCase02_PlayerSurvivesPenalty()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            // Chamber 0 blank, bullet in chamber 5
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var res = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            Assert.False(res.IsBulletHit);
            Assert.False(res.Eliminated);
            Assert.True(res.Survived);
            Assert.True(p1.IsAlive);
        }

        [Fact]
        public async Task TestCase03_CannotProcessPenaltyTwice()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var actionId = Guid.NewGuid();
            var req = new PullTriggerRequest
            {
                ActionId = actionId,
                ExpectedVersion = match.StateVersion
            };

            var res1 = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", req);
            int pullsCount1 = p1.Revolver.PullsCount;

            // Retrying with same actionId
            var res2 = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", req);

            Assert.Equal(pullsCount1, p1.Revolver.PullsCount);
            Assert.Equal(res1.StateVersion, res2.StateVersion);
            Assert.Equal(res1.PenaltyEventId, res2.PenaltyEventId);
        }

        [Fact]
        public void TestCase04_ClientCannotSendLiveOrDieResult()
        {
            // PullTriggerRequest DTO only has ActionId and ExpectedVersion.
            // Client cannot pass 'isBulletHit' or 'survived'.
            var requestProperties = typeof(PullTriggerRequest).GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("IsBulletHit", requestProperties);
            Assert.DoesNotContain("Survived", requestProperties);
            Assert.DoesNotContain("Eliminated", requestProperties);
        }

        // =========================================================================
        // GROUP 2: ANIMATION ACK & TIMEOUT (TEST CASES 5 - 9)
        // =========================================================================

        [Fact]
        public async Task TestCase05_AcknowledgeCorrectPenaltyEvent_TransitionsRound()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 3);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0); // survives
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            Assert.NotNull(pullRes.PenaltyEventId);

            // Client sends ACK
            var ackRes = await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.True(ackRes.Accepted);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
            Assert.True(match.RoundTransitionHandled);
        }

        [Fact]
        public async Task TestCase06_AcknowledgeWrongPenaltyEvent_Fails()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
                {
                    ActionId = Guid.NewGuid(),
                    PenaltyEventId = "wrong_penalty_event_id"
                }));

            Assert.Equal("WRONG_PENALTY", ex.Code);
        }

        [Fact]
        public async Task TestCase07_DuplicateAck_DoesNotTransitionTwice()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            var ackActionId = Guid.NewGuid();
            var ackReq = new PenaltyAckRequest
            {
                ActionId = ackActionId,
                PenaltyEventId = pullRes.PenaltyEventId!
            };

            var ack1 = await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", ackReq);
            int versionAfterFirst = match.StateVersion;
            int roundNumberAfterFirst = match.RoundNumber;

            // Send duplicate ACK
            var ack2 = await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", ackReq);

            Assert.Equal(versionAfterFirst, match.StateVersion);
            Assert.Equal(roundNumberAfterFirst, match.RoundNumber);
            Assert.Equal(ack1.StateVersion, ack2.StateVersion);
        }

        [Fact]
        public async Task TestCase08_TimeoutWhenClientDoesNotAck_TransitionsAutomatically()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            // Client does NOT call ACK -> server timeout triggers
            bool timeoutHandled = await h.PenaltyService.ProcessPenaltyTimeoutAsync(match.MatchId, pullRes.PenaltyEventId!);

            Assert.True(timeoutHandled);
            Assert.True(match.RoundTransitionHandled);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
        }

        [Fact]
        public async Task TestCase09_ConcurrentAckAndTimeout_DoNotTransitionTwice()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            // ACK completes first
            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            int roundAfterAck = match.RoundNumber;
            int versionAfterAck = match.StateVersion;

            // Timeout executes afterwards
            await h.PenaltyService.ProcessPenaltyTimeoutAsync(match.MatchId, pullRes.PenaltyEventId!);

            // Must not transition a second time
            Assert.Equal(roundAfterAck, match.RoundNumber);
            Assert.Equal(versionAfterAck, match.StateVersion);
        }

        // =========================================================================
        // GROUP 3: ELIMINATION & WINNER (TEST CASES 10 - 15)
        // =========================================================================

        [Fact]
        public void TestCase10_EliminatePlayerAccurately()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 3);

            var p1 = match.FindPlayer("player_01")!;
            Assert.True(p1.IsAlive);
            Assert.NotEmpty(p1.Hand);

            h.MatchService.EliminatePlayer(match, "player_01");

            Assert.False(p1.IsAlive);
            Assert.Empty(p1.Hand);
            Assert.Equal(2, match.AlivePlayers.Count);
        }

        [Fact]
        public async Task TestCase11_EliminatedPlayerCannotPlayCards()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            string currentPid = match.CurrentTurnPlayerId!;
            h.MatchService.EliminatePlayer(match, currentPid);
            h.Store.SaveMatch(match);

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                h.ActionService.PlayCardsAsync(match.MatchId, currentPid, new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { "c1" }
                }));

            Assert.Equal("NOT_YOUR_TURN", ex.Code);
        }

        [Fact]
        public async Task TestCase12_EliminatedPlayerCannotChallenge()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            string currentPid = match.CurrentTurnPlayerId!;
            h.MatchService.EliminatePlayer(match, currentPid);
            h.Store.SaveMatch(match);

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                h.ChallengeService.ChallengeAsync(match.MatchId, currentPid, new ChallengeRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    TargetPlayId = "some_play"
                }));

            Assert.Equal("NOT_YOUR_TURN", ex.Code);
        }

        [Fact]
        public async Task TestCase13_MatchContinuesIfTwoPlayersAlive()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 3); // 3 players

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0); // Hits bullet -> eliminated
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Equal(2, match.AlivePlayers.Count);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase); // Continues
            Assert.Null(match.WinnerPlayerId);
        }

        [Fact]
        public async Task TestCase14_MatchEndsIfOnePlayerRemains()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2); // 2 players

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0); // Hits bullet -> eliminated
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Single(match.AlivePlayers);
            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal("player_02", match.WinnerPlayerId);
        }

        [Fact]
        public async Task TestCase15_CannotChangeWinnerAfterMatchFinished()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            await h.MatchService.FinishMatchAsync(match, "player_02");
            Assert.Equal("player_02", match.WinnerPlayerId);

            // Attempting to finish again with a different winner must not change existing winner
            await h.MatchService.FinishMatchAsync(match, "player_01");
            Assert.Equal("player_02", match.WinnerPlayerId);
        }

        // =========================================================================
        // GROUP 4: ROUND TRANSITION (TEST CASES 16 - 20)
        // =========================================================================

        [Fact]
        public async Task TestCase16_RoundTransitionOccursProperly()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            int roundBefore = match.RoundNumber;
            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0); // survives
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Equal(roundBefore + 1, match.RoundNumber);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
        }

        [Fact]
        public async Task TestCase17_NewRoundResetsLastPlay()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.LastPlay = new LastPlay { PlayId = "old_play", CardCount = 2, PlayerId = "player_01" };
            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Null(match.LastPlay);
        }

        [Fact]
        public async Task TestCase18_DoesNotDealCardsToEliminatedPlayer()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 3);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0); // eliminated
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Empty(p1.Hand);
            Assert.Equal(5, match.FindPlayer("player_02")!.Hand.Count);
            Assert.Equal(5, match.FindPlayer("player_03")!.Hand.Count);
        }

        [Fact]
        public async Task TestCase19_CannotCreateTwoRoundsFromSamePenaltyEvent()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = "player_01";
            var p1 = match.FindPlayer("player_01")!;
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);
            h.Store.SaveMatch(match);

            var pullRes = await h.PenaltyService.PullTriggerAsync(match.MatchId, "player_01", new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            });

            // Player 1 ACKs
            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_01", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            int roundAfterFirstAck = match.RoundNumber;

            // Player 2 also ACKs same event
            await h.MatchService.AcknowledgePenaltyAsync(match.MatchId, "player_02", new PenaltyAckRequest
            {
                ActionId = Guid.NewGuid(),
                PenaltyEventId = pullRes.PenaltyEventId!
            });

            Assert.Equal(roundAfterFirstAck, match.RoundNumber);
        }

        [Fact]
        public async Task TestCase20_CannotPlayCardsDuringPenaltyResolution()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            h.Store.SaveMatch(match);

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                h.ActionService.PlayCardsAsync(match.MatchId, "player_01", new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { match.FindPlayer("player_01")!.Hand.First().CardInstanceId }
                }));

            Assert.Equal("INVALID_PHASE", ex.Code);
        }

        // =========================================================================
        // GROUP 5: MATCH RESULT (TEST CASES 21 - 25)
        // =========================================================================

        [Fact]
        public async Task TestCase21_PersistCorrectWinner()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);

            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = "player_02";

            var saved = await h.ResultService.SaveMatchResultAsync(match);

            Assert.NotNull(saved);
            Assert.Equal("player_02", saved!.WinnerId);
        }

        [Fact]
        public async Task TestCase22_PersistCorrectParticipants()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 3);

            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = "player_03";

            var saved = await h.ResultService.SaveMatchResultAsync(match);

            Assert.NotNull(saved);
            Assert.Equal(3, saved!.ParticipantIds.Count);
            Assert.Contains("player_01", saved.ParticipantIds);
            Assert.Contains("player_02", saved.ParticipantIds);
            Assert.Contains("player_03", saved.ParticipantIds);
        }

        [Fact]
        public async Task TestCase23_CannotPersistDuplicateMatchId()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);
            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = "player_01";

            var saved1 = await h.ResultService.SaveMatchResultAsync(match);
            var saved2 = await h.ResultService.SaveMatchResultAsync(match);

            Assert.Equal(saved1!.MatchId, saved2!.MatchId);
        }

        [Fact]
        public async Task TestCase24_GetMatchResultSuccessfully()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);
            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = "player_01";
            await h.ResultService.SaveMatchResultAsync(match);

            var controller = new ResultsController(h.ResultService, h.Store);
            var result = await controller.GetMatchResult(match.MatchId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<MatchResultResponse>(okResult.Value);
            Assert.Equal(match.MatchId, response.MatchId);
            Assert.Equal("player_01", response.WinnerId);
        }

        [Fact]
        public async Task TestCase25_GetMatchResultFailsWhenNotFinished()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);
            match.Phase = RoundPhase.AwaitPlayOrChallenge; // Still playing!

            var controller = new ResultsController(h.ResultService, h.Store);
            var result = await controller.GetMatchResult(match.MatchId);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            var error = Assert.IsType<GameErrorResponse>(badReq.Value);
            Assert.Equal("MATCH_NOT_FINISHED", error.Code);
        }

        // =========================================================================
        // GROUP 6: START MATCH & HANDOFF & JOIN (TEST CASES 26 - 32)
        // =========================================================================

        [Fact]
        public void TestCase26_StartMatchTwoPlayers_Succeeds()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            h.TableService.SetReady("player_02", true);

            var controller = new TablesController(h.TableService, h.MatchService, h.TicketService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "host_player") }))
                }
            };

            var actionResult = controller.StartMatch(1);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var response = Assert.IsType<StartMatchResponse>(okResult.Value);

            Assert.Equal(1, response.TableId);
            Assert.NotNull(response.MatchId);
            Assert.Equal(2, response.PlayerCount);
            Assert.Equal("WaitingForPlayers", response.Status);
        }

        [Fact]
        public void TestCase27_StartMatchFourPlayers_Succeeds()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            h.TableService.SitDown(1, 2, "player_03");
            h.TableService.SitDown(1, 3, "player_04");

            h.TableService.SetReady("player_02", true);
            h.TableService.SetReady("player_03", true);
            h.TableService.SetReady("player_04", true);

            var controller = new TablesController(h.TableService, h.MatchService, h.TicketService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "host_player") }))
                }
            };

            var actionResult = controller.StartMatch(1);
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var response = Assert.IsType<StartMatchResponse>(okResult.Value);

            Assert.Equal(4, response.PlayerCount);
        }

        [Fact]
        public void TestCase28_StartMatchOnePlayer_Fails()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");

            var controller = new TablesController(h.TableService, h.MatchService, h.TicketService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "host_player") }))
                }
            };

            var actionResult = controller.StartMatch(1);
            var badReq = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Contains("INSUFFICIENT_PLAYERS", badReq.Value?.ToString() ?? "");
        }

        [Fact]
        public void TestCase29_StartMatchNotReady_Fails()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            // player_02 has NOT ready yet

            var controller = new TablesController(h.TableService, h.MatchService, h.TicketService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "host_player") }))
                }
            };

            var actionResult = controller.StartMatch(1);
            var conflict = Assert.IsType<ConflictObjectResult>(actionResult);
            Assert.Contains("TABLE_NOT_READY", conflict.Value?.ToString() ?? "");
        }

        [Fact]
        public void TestCase30_ConcurrentStartMatchDoesNotCreateDuplicate()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            h.TableService.SetReady("player_02", true);

            var (error1, playerIds1) = h.TableService.ValidateAndStartMatch(1, "host_player");
            Assert.Null(error1);

            // Table status is now Playing -> Second attempt must be rejected
            var (error2, playerIds2) = h.TableService.ValidateAndStartMatch(1, "host_player");
            Assert.Equal("TABLE_NOT_WAITING", error2);
            Assert.Null(playerIds2);
        }

        [Fact]
        public void TestCase31_NonHostCannotStartMatch()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            h.TableService.SetReady("player_02", true);

            var controller = new TablesController(h.TableService, h.MatchService, h.TicketService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "player_02") })) // non-host
                }
            };

            var actionResult = controller.StartMatch(1);
            var statusResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        }

        [Fact]
        public async Task TestCase32_PlayerCannotUseAnotherPlayerTicket()
        {
            var h = TestHarness.Create();
            h.TableService.SitDown(1, 0, "host_player");
            h.TableService.SitDown(1, 1, "player_02");
            h.TableService.SetReady("player_02", true);

            var tableCtrl = new TablesController(h.TableService, h.MatchService, h.TicketService);
            tableCtrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "host_player") }))
                }
            };

            var startRes = (OkObjectResult)tableCtrl.StartMatch(1);
            var startData = (StartMatchResponse)startRes.Value!;

            // Player 2 gets own ticket
            tableCtrl.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "player_02") }));
            var handoffRes = (OkObjectResult)tableCtrl.GetHandoff(1);
            var handoffData = (TableHandoffResponse)handoffRes.Value!;
            string p2Ticket = handoffData.JoinTicket;

            // Player 1 tries to use Player 2's ticket
            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                h.MatchService.JoinMatchAsync(startData.MatchId, "host_player", p2Ticket));

            Assert.Equal("TICKET_OWNER_MISMATCH", ex.Code);
        }

        [Fact]
        public async Task TestCase33_AfterPullTriggerHit_GetPublicState_PenalizedPlayerIdIsNull_And_WinnerIsSet()
        {
            var h = TestHarness.Create();
            var match = CreateMatchWithPlayers(h, 2);
            var p1 = match.Players[0];
            var p2 = match.Players[1];

            // Set P1 as penalized player
            match.Phase = RoundPhase.PenaltyResolution;
            match.PenalizedPlayerId = p1.PlayerId;
            match.CurrentTurnPlayerId = p1.PlayerId;
            match.PenaltyResolved = false;
            match.RoundTransitionHandled = false;
            h.Store.SaveMatch(match);

            // Force P1 revolver bullet to chamber 0 so it explodes on trigger pull
            p1.Revolver.BulletChamber = 0;
            p1.Revolver.CurrentChamber = 0;

            // Pull trigger
            var req = new PullTriggerRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion
            };
            var res = await h.PenaltyService.PullTriggerAsync(match.MatchId, p1.PlayerId, req);

            Assert.True(res.IsBulletHit);
            Assert.True(res.Eliminated);
            Assert.False(res.Survived);
            Assert.Equal("MatchEnded", res.NextPhase);
            Assert.Equal(p2.PlayerId, res.WinnerPlayerId);

            // Query public state via MatchQueryService
            var publicState = h.QueryService.GetPublicState(match.MatchId);

            // CRITICAL: PenalizedPlayerId MUST be null so client polling never triggers penalty modal again!
            Assert.Null(publicState.PenalizedPlayerId);
            Assert.Equal("MatchEnded", publicState.Phase);
            Assert.Equal(p2.PlayerId, publicState.WinnerPlayerId);

            var p1State = publicState.Players.First(p => p.PlayerId == p1.PlayerId);
            Assert.False(p1State.IsAlive);

            var p2State = publicState.Players.First(p => p.PlayerId == p2.PlayerId);
            Assert.True(p2State.IsAlive);

            // Verify persistent MatchResult
            var savedResult = await h.ResultRepository.GetByMatchIdAsync(match.MatchId);
            Assert.NotNull(savedResult);
            Assert.Equal(p2.PlayerId, savedResult!.WinnerId);
        }
    }
}
