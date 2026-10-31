using System.Collections.Concurrent;
using Back_End.Models;
using MongoDB.Driver;

namespace Back_End.Data
{
    public class MatchResultRepository : IMatchResultRepository
    {
        private readonly IMongoCollection<MatchResult>? _collection;
        private readonly ConcurrentDictionary<string, MatchResult> _inMemoryFallback = new();
        private readonly ILogger<MatchResultRepository>? _logger;

        public MatchResultRepository(IMongoDatabase? database = null, ILogger<MatchResultRepository>? logger = null)
        {
            _logger = logger;
            if (database != null)
            {
                try
                {
                    _collection = database.GetCollection<MatchResult>("MatchResults");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Không thể kết nối collection MatchResults trong MongoDB, chuyển sang Fallback Store.");
                }
            }
        }

        public async Task<bool> SaveAsync(MatchResult result)
        {
            if (string.IsNullOrWhiteSpace(result.MatchId))
            {
                throw new ArgumentException("MatchId không được để trống.", nameof(result));
            }

            // Always update fallback cache
            _inMemoryFallback[result.MatchId] = result;

            if (_collection != null)
            {
                try
                {
                    // Chống ghi trùng MatchId theo Unique Index
                    await _collection.InsertOneAsync(result);
                    return true;
                }
                catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    _logger?.LogInformation("MatchResult cho match {MatchId} đã tồn tại trong MongoDB.", result.MatchId);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Lỗi khi lưu MatchResult vào MongoDB, kết quả đã được giữ lại trong Fallback Store.");
                    return true;
                }
            }

            return true;
        }

        public async Task<MatchResult?> GetByMatchIdAsync(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return null;
            }

            if (_collection != null)
            {
                try
                {
                    var result = await _collection.Find(r => r.MatchId == matchId).FirstOrDefaultAsync();
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Lỗi khi truy vấn MongoDB, chuyển sang tìm kiếm từ Fallback Store.");
                }
            }

            _inMemoryFallback.TryGetValue(matchId, out var fallbackResult);
            return fallbackResult;
        }
    }
}

