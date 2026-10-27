using Back_End.Models;
using MongoDB.Driver;

namespace Back_End.Data
{
    public sealed class MongoMatchResultRepository : IMatchResultRepository
    {
        public const string CollectionName = "match_results";
        public const string MatchIdIndexName = "ux_match_results_match_id";

        private readonly IMongoCollection<MatchResultDocument> _collection;

        public MongoMatchResultRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<MatchResultDocument>(CollectionName);
        }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken)
        {
            var index = new CreateIndexModel<MatchResultDocument>(
                Builders<MatchResultDocument>.IndexKeys.Ascending(document => document.MatchId),
                new CreateIndexOptions
                {
                    Name = MatchIdIndexName,
                    Unique = true
                });

            return _collection.Indexes.CreateOneAsync(
                index,
                cancellationToken: cancellationToken);
        }

        public async Task<bool> TryInsertAsync(
            MatchResultDocument document,
            CancellationToken cancellationToken)
        {
            try
            {
                await _collection.InsertOneAsync(
                    document,
                    cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoWriteException exception)
                when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<MatchResultDocument?> GetByMatchIdAsync(
            string matchId,
            CancellationToken cancellationToken) =>
            await _collection
                .Find(document => document.MatchId == matchId)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
