using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Claims;
using Back_End.Data;
using Back_End.Controllers;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Back_End.Tests
{
    public sealed class MatchResultServiceTests
    {
        private static readonly DateTimeOffset FixedNow =
            new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

        private static (InMemoryMatchStore Store, TestMatchResultRepository Repository, MatchResultService Service) CreateSystem()
        {
            var store = new InMemoryMatchStore();
            var repository = new TestMatchResultRepository();
            var clock = new FixedTimeProvider(FixedNow);
            return (
                store,
                repository,
                new MatchResultService(
                    store,
                    repository,
                    clock,
                    NullLogger<MatchResultService>.Instance));
        }

        private static GameMatch CreateEndedMatch(string? winnerPlayerId = "p1")
        {
            return new GameMatch
            {
                MatchId = "match-result-test",
                Phase = RoundPhase.MatchEnded,
                WinnerPlayerId = winnerPlayerId,
                StateVersion = 42,
                CreatedAt = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc),
                Players =
                [
                    new MatchPlayer
                    {
                        PlayerId = "p1",
                        DisplayName = "Player One",
                        SeatIndex = 0,
                        IsAlive = winnerPlayerId == "p1",
                        Hand = [new Card("secret-card-id", Card.RankKing)]
                    },
                    new MatchPlayer
                    {
                        PlayerId = "p2",
                        DisplayName = "Player Two",
                        SeatIndex = 1,
                        IsAlive = winnerPlayerId == "p2",
                        Hand = []
                    }
                ]
            };
        }

        [Fact]
        public async Task PersistsOnlyAuthoritativelyEndedMatches()
        {
            var (store, _, service) = CreateSystem();
            var match = CreateEndedMatch();
            match.Phase = RoundPhase.AwaitPlayOrChallenge;
            store.SaveMatch(match);

            await Assert.ThrowsAsync<GameRuleException>(() =>
                service.PersistCompletedMatchAsync(match.MatchId, CancellationToken.None));
        }

        [Fact]
        public async Task PersistsWinnerAndPublicParticipantSummary()
        {
            var (store, repository, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch());

            var response = await service.PersistCompletedMatchAsync(
                "match-result-test",
                CancellationToken.None);
            var document = Assert.Single(repository.Documents);

            Assert.Equal("p1", response.WinnerPlayerId);
            Assert.Equal(42, response.FinalStateVersion);
            Assert.Equal(FixedNow, response.EndedAtUtc);
            Assert.Equal(2, response.Players.Count);
            Assert.True(response.Players[0].IsWinner);
            Assert.False(response.Players[1].IsAliveAtEnd);
            Assert.Equal(0, response.Players[1].RemainingCardCountAtEnd);
            Assert.DoesNotContain("secret-card-id", JsonSerializer.Serialize(response));
            string serialized = document.ToBsonDocument().ToJson();
            Assert.DoesNotContain("secret-card-id", serialized);
            Assert.Contains("\"matchId\"", serialized);
            Assert.DoesNotContain("\"hand\"", serialized);
            Assert.DoesNotContain("\"revolver\"", serialized);
            Assert.DoesNotContain("\"token\"", serialized);
        }

        [Fact]
        public async Task PersistsCompletedMatchWithoutWinnerAsNull()
        {
            var (store, repository, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch(winnerPlayerId: null));

            var response = await service.PersistCompletedMatchAsync(
                "match-result-test",
                CancellationToken.None);

            Assert.Null(response.WinnerPlayerId);
            Assert.DoesNotContain(response.Players, player => player.IsWinner);
            Assert.Null(Assert.Single(repository.Documents).WinnerPlayerId);
        }

        [Fact]
        public async Task IdenticalRetryRetainsTheOriginalDocumentAndTimestamp()
        {
            var (store, repository, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch());

            var first = await service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None);
            var originalTimestamp = first.EndedAtUtc;
            var second = await service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None);

            Assert.Single(repository.Documents);
            Assert.Equal(originalTimestamp, second.EndedAtUtc);
            Assert.Equal(first.MatchId, second.MatchId);
            Assert.Equal(first.WinnerPlayerId, second.WinnerPlayerId);
            Assert.Equal(first.EndedAtUtc, second.EndedAtUtc);
            Assert.Equal(first.Players, second.Players);
        }

        [Fact]
        public async Task ConcurrentInsertForSameMatchCreatesOneImmutableResult()
        {
            var (store, repository, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch());

            var results = await Task.WhenAll(
                service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None),
                service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None));

            Assert.Single(repository.Documents);
            Assert.Equal(results[0].MatchId, results[1].MatchId);
            Assert.Equal(results[0].WinnerPlayerId, results[1].WinnerPlayerId);
            Assert.Equal(results[0].Players, results[1].Players);
        }

        [Fact]
        public async Task ConflictingRetryDoesNotOverwriteExistingResult()
        {
            var (store, repository, service) = CreateSystem();
            var match = CreateEndedMatch();
            store.SaveMatch(match);
            await service.PersistCompletedMatchAsync(match.MatchId, CancellationToken.None);
            var original = Assert.Single(repository.Documents);

            match.WinnerPlayerId = "p2";
            match.Players[0].IsAlive = false;
            match.Players[1].IsAlive = true;
            store.SaveMatch(match);

            await Assert.ThrowsAsync<MatchResultConflictException>(() =>
                service.PersistCompletedMatchAsync(match.MatchId, CancellationToken.None));
            Assert.Single(repository.Documents);
            Assert.Equal("p1", original.WinnerPlayerId);
        }

        [Fact]
        public async Task ResultQueryIsRestrictedToParticipantsAndHidesMissingResults()
        {
            var (store, _, service) = CreateSystem();

            Assert.Null(await service.GetForParticipantAsync(
                "unknown-match",
                "p1",
                CancellationToken.None));

            store.SaveMatch(CreateEndedMatch());
            await service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None);

            Assert.NotNull(await service.GetForParticipantAsync(
                "match-result-test",
                "p1",
                CancellationToken.None));
            Assert.Null(await service.GetForParticipantAsync(
                "match-result-test",
                "not-a-participant",
                CancellationToken.None));
        }

        [Fact]
        public async Task ResultEndpointReturnsNotFoundForMissingOrNonParticipant()
        {
            var (store, _, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch());
            await service.PersistCompletedMatchAsync("match-result-test", CancellationToken.None);
            var controller = new MatchesController(
                new MatchService(store, new DeckRoundService(new Back_End.Rules.TurnStateMachine())),
                new MatchQueryService(store),
                service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim("sub", "not-a-participant")],
                            "test"))
                    }
                }
            };

            var unauthorizedResult = await controller.GetResult(
                "match-result-test",
                CancellationToken.None);
            Assert.IsType<NotFoundResult>(unauthorizedResult.Result);

            var missingResult = await controller.GetResult(
                "unknown-match",
                CancellationToken.None);
            Assert.IsType<NotFoundResult>(missingResult.Result);

            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            var unauthenticatedResult = await controller.GetResult(
                "match-result-test",
                CancellationToken.None);
            Assert.IsType<UnauthorizedObjectResult>(unauthenticatedResult.Result);
        }

        [Fact]
        public async Task ResultQueryPersistenceFailureIsSurfacedAsServerError()
        {
            var (store, repository, service) = CreateSystem();
            store.SaveMatch(CreateEndedMatch());
            repository.FailNextRead = true;

            var exception = await Assert.ThrowsAsync<MatchResultPersistenceException>(() =>
                service.GetForParticipantAsync(
                    "match-result-test",
                    "p1",
                    CancellationToken.None));

            Assert.IsType<MongoException>(exception.InnerException);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTimeOffset now) => _now = now;

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }

    internal sealed class TestMatchResultRepository : IMatchResultRepository
    {
        private readonly ConcurrentDictionary<string, MatchResultDocument> _documents = new(StringComparer.Ordinal);

        public bool FailNextInsert { get; set; }
        public bool FailNextRead { get; set; }
        public IReadOnlyCollection<MatchResultDocument> Documents => _documents.Values.ToArray();

        public Task EnsureIndexesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TryInsertAsync(
            MatchResultDocument document,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailNextInsert)
            {
                FailNextInsert = false;
                throw new MongoException("Simulated MongoDB failure.");
            }

            return Task.FromResult(_documents.TryAdd(document.MatchId, Clone(document)));
        }

        public Task<MatchResultDocument?> GetByMatchIdAsync(
            string matchId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailNextRead)
            {
                FailNextRead = false;
                throw new MongoException("Simulated MongoDB read failure.");
            }

            return Task.FromResult(
                _documents.TryGetValue(matchId, out var document)
                    ? Clone(document)
                    : null);
        }

        private static MatchResultDocument Clone(MatchResultDocument source) =>
            new()
            {
                MatchId = source.MatchId,
                WinnerPlayerId = source.WinnerPlayerId,
                MatchCreatedAtUtc = source.MatchCreatedAtUtc,
                EndedAtUtc = source.EndedAtUtc,
                FinalStateVersion = source.FinalStateVersion,
                Players = source.Players.Select(player => new MatchResultPlayerDocument
                {
                    PlayerId = player.PlayerId,
                    DisplayName = player.DisplayName,
                    SeatIndex = player.SeatIndex,
                    IsAliveAtEnd = player.IsAliveAtEnd,
                    IsWinner = player.IsWinner,
                    RemainingCardCountAtEnd = player.RemainingCardCountAtEnd
                }).ToList()
            };
    }
}
