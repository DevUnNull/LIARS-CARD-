using Back_End.Data;
using Back_End.Models;
using MongoDB.Driver;
using Xunit;

namespace Back_End.Tests
{
    public sealed class MongoMatchResultRepositoryIntegrationTests
    {
        [MongoIntegrationFact]
        public async Task UniqueMatchIdIndexAllowsOnlyOneInsertAndReadsPersistedDocument()
        {
            string connectionString =
                Environment.GetEnvironmentVariable("MATCH_RESULTS_MONGO_URI") ??
                "mongodb://localhost:27017";
            string databaseName = $"liars_card_be405_test_{Guid.NewGuid():N}";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var repository = new MongoMatchResultRepository(database);

            try
            {
                await repository.EnsureIndexesAsync(CancellationToken.None);
                var document = new MatchResultDocument
                {
                    MatchId = "integration-match",
                    WinnerPlayerId = "player-1",
                    MatchCreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                    EndedAtUtc = DateTime.UtcNow,
                    FinalStateVersion = 12,
                    Players =
                    [
                        new MatchResultPlayerDocument
                        {
                            PlayerId = "player-1",
                            DisplayName = "Player One",
                            SeatIndex = 0,
                            IsAliveAtEnd = true,
                            IsWinner = true,
                            RemainingCardCountAtEnd = 3
                        }
                    ]
                };

                var insertResults = await Task.WhenAll(
                    repository.TryInsertAsync(document, CancellationToken.None),
                    repository.TryInsertAsync(document, CancellationToken.None));

                Assert.Single(insertResults.Where(inserted => inserted));
                Assert.Single(insertResults.Where(inserted => !inserted));

                var persisted = await repository.GetByMatchIdAsync(
                    document.MatchId,
                    CancellationToken.None);
                Assert.NotNull(persisted);
                Assert.Equal(document.WinnerPlayerId, persisted.WinnerPlayerId);
                Assert.Equal(document.FinalStateVersion, persisted.FinalStateVersion);
                Assert.Equal(document.Players[0].PlayerId, Assert.Single(persisted.Players).PlayerId);
                Assert.Equal(
                    1,
                    await database.GetCollection<MatchResultDocument>(MongoMatchResultRepository.CollectionName)
                        .CountDocumentsAsync(Builders<MatchResultDocument>.Filter.Empty));
            }
            finally
            {
                await client.DropDatabaseAsync(databaseName);
            }
        }

        private sealed class MongoIntegrationFactAttribute : FactAttribute
        {
            public MongoIntegrationFactAttribute()
            {
                if (!string.Equals(
                        Environment.GetEnvironmentVariable("RUN_MATCH_RESULTS_MONGO_INTEGRATION"),
                        "1",
                        StringComparison.Ordinal))
                {
                    Skip = "Set RUN_MATCH_RESULTS_MONGO_INTEGRATION=1 to run against a local MongoDB instance.";
                }
            }
        }
    }
}
