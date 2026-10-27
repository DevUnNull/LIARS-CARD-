using System.Collections.Concurrent;
using System.Text.Json;
using Back_End.Exceptions;
using Back_End.DTOs;
using Back_End.Hubs;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;
using Back_End.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Back_End.Tests
{
    public class PenaltyAnimationServiceTests
    {
        private sealed record TestSystem(
            InMemoryMatchStore Store,
            TestPenaltyAnimationNotifier Notifier,
            TestTimeProvider Clock,
            PenaltyAnimationService AnimationService,
            ChallengeService ChallengeService,
            DeckActionService ActionService,
            MatchService MatchService,
            TestGameplayStateNotifier GameplayNotifier,
            TestMatchResultRepository ResultRepository);

        private static TestSystem CreateSystem(int timeoutSeconds = 30)
        {
            var store = new InMemoryMatchStore();
            var notifier = new TestPenaltyAnimationNotifier();
            var clock = new TestTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
            var ruleEngine = new DeckRuleEngine();
            var turnStateMachine = new TurnStateMachine();
            var roundService = new DeckRoundService(turnStateMachine);
            var penaltyService = new PenaltyService(ruleEngine);
            var animationService = new PenaltyAnimationService(
                store,
                notifier,
                clock,
                Options.Create(new PenaltyAnimationOptions { AckTimeoutSeconds = timeoutSeconds }),
                NullLogger<PenaltyAnimationService>.Instance);
            var gameplayNotifier = new TestGameplayStateNotifier();
            var resultRepository = new TestMatchResultRepository();
            var resultService = new MatchResultService(
                store,
                resultRepository,
                clock,
                NullLogger<MatchResultService>.Instance);

            return new TestSystem(
                store,
                notifier,
                clock,
                animationService,
                new ChallengeService(store, ruleEngine, penaltyService, roundService, animationService, gameplayNotifier, resultService),
                new DeckActionService(store, ruleEngine, turnStateMachine),
                new MatchService(store, roundService),
                gameplayNotifier,
                resultRepository);
        }

        private static GameMatch CreateActiveMatch(TestSystem system)
        {
            var match = system.MatchService.CreateMatch("match_ack", new List<string> { "p1", "p2", "p3" });
            foreach (var player in match.Players)
            {
                player.IsReady = true;
            }

            new DeckRoundService(new TurnStateMachine()).StartRound(
                match,
                preferredStartingSeat: 0,
                fixedTableRank: Card.RankKing);
            system.Store.SaveMatch(match);
            return match;
        }

        private static async Task<(ChallengeResponse Response, GameMatch Match)> CreatePenaltyEventAsync(TestSystem system)
        {
            var match = CreateActiveMatch(system);
            string playOwnerId = match.CurrentTurnPlayerId!;
            var playOwner = match.FindPlayer(playOwnerId)!;
            playOwner.Hand[0] = new Card("penalty-card", Card.RankKing);

            var play = await system.ActionService.PlayCardsAsync(
                match.MatchId,
                playOwnerId,
                new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { "penalty-card" }
                });

            string challengerId = match.CurrentTurnPlayerId!;
            var challengeRequest = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = play.PlayId
            };

            var response = await system.ChallengeService.ChallengeAsync(
                match.MatchId,
                challengerId,
                challengeRequest);

            return (response, match);
        }

        private static async Task<(ChallengeResponse Response, GameMatch Match, ChallengeRequest Request, string ChallengerId, string PenalizedPlayerId)> CreateEliminationEventAsync(
            TestSystem system,
            int playerCount)
        {
            var (match, request, challengerId) =
                await CreateEliminationChallengeContextAsync(system, playerCount);
            var response = await system.ChallengeService.ChallengeAsync(
                match.MatchId,
                challengerId,
                request);

            return (response, match, request, challengerId, challengerId);
        }

        private static async Task<(GameMatch Match, ChallengeRequest Request, string ChallengerId)> CreateEliminationChallengeContextAsync(
            TestSystem system,
            int playerCount)
        {
            var match = system.MatchService.CreateMatch(
                $"match_elimination_{playerCount}",
                Enumerable.Range(1, playerCount).Select(index => $"p{index}").ToList());
            foreach (var player in match.Players)
            {
                player.IsReady = true;
            }

            new DeckRoundService(new TurnStateMachine()).StartRound(
                match,
                preferredStartingSeat: 0,
                fixedTableRank: Card.RankKing);
            system.Store.SaveMatch(match);

            string playOwnerId = match.CurrentTurnPlayerId!;
            var playOwner = match.FindPlayer(playOwnerId)!;
            playOwner.Hand[0] = new Card("elimination-card", Card.RankKing);

            var play = await system.ActionService.PlayCardsAsync(
                match.MatchId,
                playOwnerId,
                new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { "elimination-card" }
                });

            string challengerId = match.CurrentTurnPlayerId!;
            var challenger = match.FindPlayer(challengerId)!;
            challenger.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0);

            var request = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = play.PlayId
            };
            return (match, request, challengerId);
        }

        [Fact]
        public async Task AllRequiredPlayersAcknowledgingCompletesSessionOnce()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            var session = system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!;

            Assert.Equal(3, session.RequiredAckCount);
            Assert.Equal(new[] { "p1", "p2", "p3" }, session.RequiredPlayerIds.OrderBy(id => id));

            foreach (var player in match.Players)
            {
                await system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    player.PlayerId,
                    CancellationToken.None);
            }

            Assert.Equal(PenaltyAnimationStatus.Completed, session.Status);
            Assert.Equal(3, session.AcknowledgedCount);
            Assert.Single(system.Notifier.EndedEvents);
        }

        [Fact]
        public async Task OfflinePlayerRemainsRequiredUntilServerTimeout()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            var session = system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!;

            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);
            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p2",
                CancellationToken.None);

            system.Clock.Advance(TimeSpan.FromSeconds(29));
            Assert.False(await system.AnimationService.TryTimeoutAsync(
                match.MatchId,
                response.PenaltyEventId,
                CancellationToken.None));
            Assert.Equal(PenaltyAnimationStatus.Pending, session.Status);

            system.Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(await system.AnimationService.TryTimeoutAsync(
                match.MatchId,
                response.PenaltyEventId,
                CancellationToken.None));

            Assert.Equal(PenaltyAnimationStatus.TimedOut, session.Status);
            Assert.Equal(2, session.AcknowledgedCount);
            Assert.Single(system.Notifier.EndedEvents);
        }

        [Fact]
        public async Task DuplicateAcknowledgementDoesNotIncreaseCount()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);

            var first = await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);
            var duplicate = await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);

            Assert.True(first.Acknowledged);
            Assert.False(duplicate.Acknowledged);
            Assert.Equal(1, duplicate.AcknowledgedCount);
            Assert.Empty(system.Notifier.EndedEvents);
        }

        [Fact]
        public async Task NonMemberAndUnknownSessionAreRejected()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);

            await Assert.ThrowsAsync<HubException>(() =>
                system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    "not-a-player",
                    CancellationToken.None));

            await Assert.ThrowsAsync<HubException>(() =>
                system.AnimationService.AcknowledgeAsync(
                    "wrong-match",
                    response.PenaltyEventId,
                    "p1",
                    CancellationToken.None));

            match.Players.Add(new MatchPlayer { PlayerId = "late-roster-player" });
            await Assert.ThrowsAsync<HubException>(() =>
                system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    "late-roster-player",
                    CancellationToken.None));

            await Assert.ThrowsAsync<HubException>(() =>
                system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    Guid.NewGuid(),
                    "p1",
                    CancellationToken.None));
        }

        [Fact]
        public async Task LateAcknowledgementTimesOutInsteadOfCompleting()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            system.Clock.Advance(TimeSpan.FromSeconds(30));

            var ack = await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);

            Assert.False(ack.Acknowledged);
            Assert.Equal(nameof(PenaltyAnimationStatus.TimedOut), ack.Status);
            Assert.Equal(0, ack.AcknowledgedCount);
            Assert.Single(system.Notifier.EndedEvents);
        }

        [Fact]
        public async Task ConcurrentFinalAcknowledgementAndTimeoutCommitOnlyOneTerminalState()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);

            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);
            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p2",
                CancellationToken.None);
            system.Clock.Advance(TimeSpan.FromSeconds(30));
            int roundNumber = match.RoundNumber;
            int stateVersion = match.StateVersion;
            var winnerPlayerId = match.WinnerPlayerId;

            await Task.WhenAll(
                system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    "p3",
                    CancellationToken.None),
                system.AnimationService.TryTimeoutAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    CancellationToken.None));

            var session = system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!;
            Assert.Equal(PenaltyAnimationStatus.TimedOut, session.Status);
            Assert.Single(system.Notifier.EndedEvents);
            Assert.Equal(roundNumber, match.RoundNumber);
            Assert.Equal(stateVersion, match.StateVersion);
            Assert.Equal(winnerPlayerId, match.WinnerPlayerId);
            Assert.Single(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task AcknowledgementAfterTerminalDoesNotChangeSessionOrRepublish()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);

            foreach (var player in match.Players)
            {
                await system.AnimationService.AcknowledgeAsync(
                    match.MatchId,
                    response.PenaltyEventId,
                    player.PlayerId,
                    CancellationToken.None);
            }

            var session = system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!;
            var terminalAt = session.CompletedAtUtc;
            var lateAck = await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);

            Assert.False(lateAck.Acknowledged);
            Assert.Equal(PenaltyAnimationStatus.Completed, session.Status);
            Assert.Equal(terminalAt, session.CompletedAtUtc);
            Assert.Single(system.Notifier.EndedEvents);
        }

        [Fact]
        public async Task ReconnectSnapshotReturnsPendingAndLatestTerminalOnly()
        {
            var system = CreateSystem();
            var (firstResponse, match) = await CreatePenaltyEventAsync(system);

            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                firstResponse.PenaltyEventId,
                "p1",
                CancellationToken.None);
            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                firstResponse.PenaltyEventId,
                "p2",
                CancellationToken.None);
            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                firstResponse.PenaltyEventId,
                "p3",
                CancellationToken.None);

            var laterSession = system.AnimationService.CreateSession(
                match,
                Guid.NewGuid(),
                new PenaltyResult
                {
                    ChallengeSucceeded = true,
                    PenalizedPlayerId = "p1",
                    Survived = true,
                    NextPhase = match.Phase.ToString()
                });

            var snapshot = await system.AnimationService.GetReconnectSnapshotAsync(
                match.MatchId,
                "p1",
                CancellationToken.None);

            Assert.Equal(2, snapshot.Count);
            Assert.Contains(snapshot, item =>
                item.Event.PenaltyEventId == laterSession.PenaltyEventId &&
                item.Status == nameof(PenaltyAnimationStatus.Pending));
            Assert.Contains(snapshot, item =>
                item.Event.PenaltyEventId == firstResponse.PenaltyEventId &&
                item.Status == nameof(PenaltyAnimationStatus.Completed));
        }

        [Fact]
        public async Task AckAndTimeoutDoNotMutateGameplayState()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            var phase = match.Phase;
            var version = match.StateVersion;
            var roundNumber = match.RoundNumber;
            var winner = match.WinnerPlayerId;
            var alive = match.Players.ToDictionary(player => player.PlayerId, player => player.IsAlive);

            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);
            system.Clock.Advance(TimeSpan.FromSeconds(30));
            await system.AnimationService.TryTimeoutAsync(
                match.MatchId,
                response.PenaltyEventId,
                CancellationToken.None);

            Assert.Equal(phase, match.Phase);
            Assert.Equal(version, match.StateVersion);
            Assert.Equal(roundNumber, match.RoundNumber);
            Assert.Equal(winner, match.WinnerPlayerId);
            Assert.Equal(alive, match.Players.ToDictionary(player => player.PlayerId, player => player.IsAlive));
            Assert.Single(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task ChallengeRetryReusesEventIdAndDoesNotRepublishStart()
        {
            var system = CreateSystem();
            var match = CreateActiveMatch(system);
            string playOwnerId = match.CurrentTurnPlayerId!;
            var playOwner = match.FindPlayer(playOwnerId)!;
            playOwner.Hand[0] = new Card("retry-card", Card.RankKing);

            var play = await system.ActionService.PlayCardsAsync(
                match.MatchId,
                playOwnerId,
                new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { "retry-card" }
                });

            var request = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = play.PlayId
            };
            string challengerId = match.CurrentTurnPlayerId!;

            var responses = await Task.WhenAll(
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request),
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request));
            var first = responses[0];
            var retry = responses[1];

            Assert.NotEqual(Guid.Empty, first.PenaltyEventId);
            Assert.Equal(first.PenaltyEventId, retry.PenaltyEventId);
            Assert.Single(system.Notifier.StartedEvents);
            Assert.Single(system.Store.GetPenaltyAnimations(match.MatchId));
        }

        [Fact]
        public async Task StoreRejectsDuplicatePenaltyEventId()
        {
            var system = CreateSystem();
            var match = CreateActiveMatch(system);
            var penaltyEventId = Guid.NewGuid();
            var result = new PenaltyResult
            {
                ChallengeSucceeded = true,
                PenalizedPlayerId = "p1",
                Survived = true
            };

            var first = system.AnimationService.CreateSession(match, penaltyEventId, result);
            var secondCreated = system.Store.TryCreatePenaltyAnimation(first);

            Assert.False(secondCreated);
            Assert.Single(system.Store.GetPenaltyAnimations(match.MatchId));
        }

        [Fact]
        public async Task ChallengeContinuesRoundWithoutWaitingForAcknowledgements()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            var session = system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!;

            Assert.Equal(PenaltyAnimationStatus.Pending, session.Status);
            Assert.Equal(2, match.RoundNumber);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
            var roundStarted = Assert.Single(system.GameplayNotifier.RoundStartedEvents);
            Assert.Equal(match.MatchId, roundStarted.MatchId);
            Assert.Equal(response.PenaltyEventId, roundStarted.PenaltyEventId);
            Assert.Equal(match.RoundNumber, roundStarted.RoundNumber);
            Assert.Equal(match.StateVersion, roundStarted.StateVersion);
            Assert.Equal(match.CurrentTurnPlayerId, roundStarted.CurrentTurnPlayerId);

            var snapshot = await new MatchQueryService(system.Store)
                .GetPublicStateAsync(match.MatchId, "p1");
            Assert.Equal(2, snapshot.RoundNumber);
            Assert.Equal(match.StateVersion, snapshot.StateVersion);
            Assert.Equal(nameof(RoundPhase.AwaitPlayOrChallenge), snapshot.Phase);
            Assert.Equal(match.CurrentTurnPlayerId, snapshot.CurrentTurnPlayerId);
        }

        [Fact]
        public async Task EliminatingOnePlayerContinuesMatchAndBroadcastsAfterStateCommit()
        {
            var system = CreateSystem();
            var (response, match, _, _, eliminatedPlayerId) =
                await CreateEliminationEventAsync(system, playerCount: 3);

            var eliminated = match.FindPlayer(eliminatedPlayerId)!;
            Assert.False(eliminated.IsAlive);
            Assert.Empty(eliminated.Hand);
            Assert.Equal(2, match.AlivePlayers.Count);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
            Assert.Null(match.WinnerPlayerId);
            Assert.Equal(2, match.RoundNumber);

            var eliminatedEvent = Assert.Single(system.GameplayNotifier.EliminatedEvents);
            Assert.Equal(response.StateVersion, eliminatedEvent.StateVersion);
            Assert.Equal(eliminatedPlayerId, eliminatedEvent.PlayerId);
            Assert.Empty(system.GameplayNotifier.EndedEvents);
            Assert.Single(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task LastSurvivorIsAuthoritativeWinnerAndMatchEnds()
        {
            var system = CreateSystem();
            var (response, match, _, _, eliminatedPlayerId) =
                await CreateEliminationEventAsync(system, playerCount: 2);

            string winnerId = match.AlivePlayers.Single().PlayerId;
            Assert.False(match.FindPlayer(eliminatedPlayerId)!.IsAlive);
            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal(winnerId, match.WinnerPlayerId);

            var endedEvent = Assert.Single(system.GameplayNotifier.EndedEvents);
            Assert.Equal(match.MatchId, endedEvent.MatchId);
            Assert.Equal(response.StateVersion, endedEvent.StateVersion);
            Assert.Equal(winnerId, endedEvent.WinnerPlayerId);
            Assert.Empty(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task EliminatedPlayerCannotPlayCards()
        {
            var system = CreateSystem();
            var (_, match, _, _, eliminatedPlayerId) =
                await CreateEliminationEventAsync(system, playerCount: 3);
            var eliminatedPlayer = match.FindPlayer(eliminatedPlayerId)!;

            var exception = await Assert.ThrowsAsync<GameRuleException>(() =>
                system.ActionService.PlayCardsAsync(
                    match.MatchId,
                    eliminatedPlayerId,
                    new PlayCardsRequest
                    {
                        ActionId = Guid.NewGuid(),
                        ExpectedVersion = match.StateVersion,
                        CardInstanceIds = new List<string> { "any-card" }
                    }));

            Assert.Equal("NOT_YOUR_TURN", exception.Code);
        }

        [Fact]
        public async Task DuplicateChallengeDoesNotRebroadcastEliminationOrMatchEnded()
        {
            var system = CreateSystem();
            var (response, match, request, challengerId, _) =
                await CreateEliminationEventAsync(system, playerCount: 2);
            var stateVersion = match.StateVersion;

            var retry = await system.ChallengeService.ChallengeAsync(
                match.MatchId,
                challengerId,
                request);

            Assert.Equal(response.StateVersion, retry.StateVersion);
            Assert.Equal(response.StateVersion, stateVersion);
            Assert.Single(system.GameplayNotifier.EliminatedEvents);
            Assert.Single(system.GameplayNotifier.EndedEvents);
            Assert.Empty(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task PersistenceFailureCanRetryCachedChallengeWithoutRepeatingGameplay()
        {
            var system = CreateSystem();
            var (match, request, challengerId) =
                await CreateEliminationChallengeContextAsync(system, playerCount: 2);
            system.ResultRepository.FailNextInsert = true;
            int roundNumberBeforeChallenge = match.RoundNumber;
            int pullsBeforeChallenge = match.FindPlayer(challengerId)!.Revolver.PullsCount;

            await Assert.ThrowsAsync<MatchResultPersistenceException>(() =>
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request));

            int roundNumberAfterFirstRequest = match.RoundNumber;
            int pullsAfterFirstRequest = match.FindPlayer(challengerId)!.Revolver.PullsCount;
            Assert.Equal(roundNumberBeforeChallenge, roundNumberAfterFirstRequest);
            Assert.Equal(pullsBeforeChallenge + 1, pullsAfterFirstRequest);
            Assert.Empty(system.GameplayNotifier.EndedEvents);

            var retry = await system.ChallengeService.ChallengeAsync(
                match.MatchId,
                challengerId,
                request);

            Assert.Equal(roundNumberAfterFirstRequest, match.RoundNumber);
            Assert.Equal(pullsAfterFirstRequest, match.FindPlayer(challengerId)!.Revolver.PullsCount);
            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal(match.StateVersion, retry.StateVersion);
            Assert.Single(system.GameplayNotifier.EndedEvents);
            Assert.Single(system.ResultRepository.Documents);

            await system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request);
            Assert.Single(system.GameplayNotifier.EndedEvents);
            Assert.Single(system.ResultRepository.Documents);
        }

        [Fact]
        public async Task DuplicateContinuingChallengeDoesNotStartAnotherRound()
        {
            var system = CreateSystem();
            var (_, match, request, challengerId, _) =
                await CreateEliminationEventAsync(system, playerCount: 3);
            var roundNumber = match.RoundNumber;
            var stateVersion = match.StateVersion;

            var retry = await system.ChallengeService.ChallengeAsync(
                match.MatchId,
                challengerId,
                request);

            Assert.Equal(roundNumber, match.RoundNumber);
            Assert.Equal(stateVersion, match.StateVersion);
            Assert.Equal(match.Phase.ToString(), retry.NextPhase);
            Assert.Single(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task ConcurrentDuplicateContinuingChallengesStartRoundExactlyOnce()
        {
            var system = CreateSystem();
            var (match, request, challengerId) =
                await CreateEliminationChallengeContextAsync(system, playerCount: 3);

            var responses = await Task.WhenAll(
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request),
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request));

            Assert.Equal(responses[0].PenaltyEventId, responses[1].PenaltyEventId);
            Assert.Equal(2, match.RoundNumber);
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
            Assert.Equal(responses[0].StateVersion, match.StateVersion);
            Assert.Single(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task ReadyPlayersCannotRestartAnEndedMatch()
        {
            var system = CreateSystem();
            var match = CreateActiveMatch(system);
            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = "p1";
            var roundNumber = match.RoundNumber;
            var stateVersion = match.StateVersion;
            system.Store.SaveMatch(match);

            await system.MatchService.SetReadyAsync(match.MatchId, "p1", true);
            await system.MatchService.SetReadyAsync(match.MatchId, "p2", true);
            await system.MatchService.SetReadyAsync(match.MatchId, "p3", true);

            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal("p1", match.WinnerPlayerId);
            Assert.Equal(roundNumber, match.RoundNumber);
            Assert.Equal(stateVersion, match.StateVersion);
        }

        [Fact]
        public async Task ConcurrentDuplicateFinalChallengesCommitAndBroadcastOnce()
        {
            var system = CreateSystem();
            var match = system.MatchService.CreateMatch("match_parallel_end", new List<string> { "p1", "p2" });
            foreach (var player in match.Players)
            {
                player.IsReady = true;
            }

            new DeckRoundService(new TurnStateMachine()).StartRound(match, 0, Card.RankKing);
            system.Store.SaveMatch(match);
            var owner = match.FindPlayer(match.CurrentTurnPlayerId!)!;
            owner.Hand[0] = new Card("parallel-card", Card.RankKing);
            var play = await system.ActionService.PlayCardsAsync(
                match.MatchId,
                owner.PlayerId,
                new PlayCardsRequest
                {
                    ActionId = Guid.NewGuid(),
                    ExpectedVersion = match.StateVersion,
                    CardInstanceIds = new List<string> { "parallel-card" }
                });

            string challengerId = match.CurrentTurnPlayerId!;
            match.FindPlayer(challengerId)!.Revolver = new PlayerRevolver(0, 0);
            var request = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = play.PlayId
            };

            var responses = await Task.WhenAll(
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request),
                system.ChallengeService.ChallengeAsync(match.MatchId, challengerId, request));

            Assert.Equal(responses[0].StateVersion, responses[1].StateVersion);
            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal(owner.PlayerId, match.WinnerPlayerId);
            Assert.Single(system.GameplayNotifier.EndedEvents);
            Assert.Empty(system.GameplayNotifier.RoundStartedEvents);
        }

        [Fact]
        public async Task ReconnectStateSnapshotContainsAuthoritativeEliminationAndWinner()
        {
            var system = CreateSystem();
            var (_, match, _, _, eliminatedPlayerId) =
                await CreateEliminationEventAsync(system, playerCount: 2);
            var snapshot = await new MatchQueryService(system.Store)
                .GetPublicStateAsync(match.MatchId, "p1");

            Assert.Equal("p1", snapshot.CurrentPlayerId);
            Assert.Equal(nameof(RoundPhase.MatchEnded), snapshot.Phase);
            Assert.Equal(match.WinnerPlayerId, snapshot.WinnerPlayerId);
            Assert.False(snapshot.Players.Single(player => player.PlayerId == eliminatedPlayerId).IsAlive);
        }

        [Fact]
        public async Task MatchEndedSnapshotDoesNotInventWinnerWhenNoneIsStored()
        {
            var system = CreateSystem();
            var match = CreateActiveMatch(system);
            match.Phase = RoundPhase.MatchEnded;
            match.WinnerPlayerId = null;
            foreach (var player in match.Players)
            {
                player.IsAlive = false;
            }

            system.Store.SaveMatch(match);
            var snapshot = await new MatchQueryService(system.Store)
                .GetPublicStateAsync(match.MatchId, "p1");

            Assert.Equal(nameof(RoundPhase.MatchEnded), snapshot.Phase);
            Assert.Null(snapshot.WinnerPlayerId);
            Assert.Empty(snapshot.Players.Where(player => player.IsAlive));
        }

        [Fact]
        public async Task StartedEventUsesCamelCaseAndContainsNoChamberData()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);
            var started = Assert.Single(system.Notifier.StartedEvents);
            string json = JsonSerializer.Serialize(started, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Contains("\"matchId\"", json);
            Assert.Contains("\"penaltyEventId\"", json);
            Assert.Contains("\"deadlineUtc\"", json);
            Assert.DoesNotContain("Chamber", json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(response.PenaltyEventId, started.PenaltyEventId);
            Assert.Equal(match.StateVersion, started.StateVersion);
        }

        [Fact]
        public async Task SignalRPublishFailureDoesNotRollbackChallengeOrSession()
        {
            var system = CreateSystem();
            system.Notifier.ThrowOnPublish = true;

            var (response, match) = await CreatePenaltyEventAsync(system);

            Assert.NotEqual(Guid.Empty, response.PenaltyEventId);
            Assert.NotNull(system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId));
            Assert.True(match.RoundNumber >= 2);
        }

        [Fact]
        public async Task TerminalPublishFailureDoesNotRollbackTerminalState()
        {
            var system = CreateSystem();
            var (response, match) = await CreatePenaltyEventAsync(system);

            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p1",
                CancellationToken.None);
            await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p2",
                CancellationToken.None);
            system.Notifier.ThrowOnPublish = true;

            var finalAck = await system.AnimationService.AcknowledgeAsync(
                match.MatchId,
                response.PenaltyEventId,
                "p3",
                CancellationToken.None);

            Assert.Equal(nameof(PenaltyAnimationStatus.Completed), finalAck.Status);
            Assert.Equal(
                PenaltyAnimationStatus.Completed,
                system.Store.GetPenaltyAnimation(match.MatchId, response.PenaltyEventId)!.Status);
        }

        [Fact]
        public async Task HostedTimeoutWorkerStopsCleanly()
        {
            var system = CreateSystem();
            var worker = new PenaltyAnimationTimeoutService(
                system.Store,
                system.AnimationService,
                NullLogger<PenaltyAnimationTimeoutService>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await worker.StopAsync(CancellationToken.None);
        }

        [Fact]
        public void PenaltyHubRequiresAuthentication()
        {
            Assert.NotNull(typeof(PenaltyHub).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        }
    }

    internal sealed class TestPenaltyAnimationNotifier : IPenaltyAnimationNotifier
    {
        private readonly ConcurrentQueue<PenaltyAnimationStarted> _startedEvents = new();
        private readonly ConcurrentQueue<PenaltyAnimationEnded> _endedEvents = new();

        public IReadOnlyCollection<PenaltyAnimationStarted> StartedEvents => _startedEvents.ToArray();
        public IReadOnlyCollection<PenaltyAnimationEnded> EndedEvents => _endedEvents.ToArray();
        public bool ThrowOnPublish { get; set; }

        public Task PublishStartedAsync(PenaltyAnimationStarted message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("Simulated SignalR delivery failure.");
            }

            _startedEvents.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task PublishEndedAsync(PenaltyAnimationEnded message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("Simulated SignalR delivery failure.");
            }

            _endedEvents.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    internal sealed class TestGameplayStateNotifier : IGameplayStateNotifier
    {
        private readonly ConcurrentQueue<PlayerEliminatedEvent> _eliminatedEvents = new();
        private readonly ConcurrentQueue<MatchEndedEvent> _endedEvents = new();
        private readonly ConcurrentQueue<RoundStartedEvent> _roundStartedEvents = new();

        public IReadOnlyCollection<PlayerEliminatedEvent> EliminatedEvents => _eliminatedEvents.ToArray();
        public IReadOnlyCollection<MatchEndedEvent> EndedEvents => _endedEvents.ToArray();
        public IReadOnlyCollection<RoundStartedEvent> RoundStartedEvents => _roundStartedEvents.ToArray();

        public Task PublishPlayerEliminatedAsync(
            PlayerEliminatedEvent message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _eliminatedEvents.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task PublishMatchEndedAsync(
            MatchEndedEvent message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _endedEvents.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task PublishRoundStartedAsync(
            RoundStartedEvent message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _roundStartedEvents.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    internal sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
