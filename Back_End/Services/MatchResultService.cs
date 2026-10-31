using Back_End.Data;
using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;

namespace Back_End.Services
{
    public class MatchResultService
    {
        private readonly IMatchResultRepository _matchResultRepository;
        private readonly IMatchStore? _matchStore;
        private readonly ILogger<MatchResultService>? _logger;

        public MatchResultService(
            IMatchResultRepository matchResultRepository,
            IMatchStore? matchStore = null,
            ILogger<MatchResultService>? logger = null)
        {
            _matchResultRepository = matchResultRepository;
            _matchStore = matchStore;
            _logger = logger;
        }

        /// <summary>
        /// Lưu kết quả trận đấu khi trận đấu kết thúc hợp lệ.
        /// Nghiệp vụ server tự động kích hoạt, không cho client tự gửi.
        /// </summary>
        public async Task<MatchResult?> SaveMatchResultAsync(GameMatch match)
        {
            if (match.Phase != RoundPhase.MatchEnded)
            {
                _logger?.LogWarning("Không thể lưu kết quả vì trận {MatchId} chưa kết thúc (Phase: {Phase}).", match.MatchId, match.Phase);
                return null;
            }

            // Kiểm tra xem đã lưu chưa
            var existing = await _matchResultRepository.GetByMatchIdAsync(match.MatchId);
            if (existing != null)
            {
                return existing;
            }

            var result = new MatchResult
            {
                MatchId = match.MatchId,
                WinnerId = match.WinnerPlayerId,
                ParticipantIds = match.Players.Select(p => p.PlayerId).Distinct().ToList(),
                StartedAt = match.StartedAt ?? match.CreatedAt,
                EndedAt = match.EndedAt ?? DateTime.UtcNow,
                TotalRounds = match.RoundNumber,
                CreatedAt = DateTime.UtcNow
            };

            await _matchResultRepository.SaveAsync(result);
            _logger?.LogInformation("Đã lưu kết quả trận đấu {MatchId}. Winner: {WinnerId}", match.MatchId, match.WinnerPlayerId);
            return result;
        }

        /// <summary>
        /// Xem kết quả trận đấu đã hoàn thành.
        /// </summary>
        public async Task<MatchResultResponse?> GetMatchResultAsync(string matchId)
        {
            var result = await _matchResultRepository.GetByMatchIdAsync(matchId);
            if (result != null)
            {
                return new MatchResultResponse
                {
                    MatchId = result.MatchId,
                    WinnerId = result.WinnerId,
                    ParticipantIds = result.ParticipantIds,
                    TotalRounds = result.TotalRounds,
                    StartedAt = result.StartedAt,
                    EndedAt = result.EndedAt
                };
            }

            // Fallback: nếu vừa kết thúc trên MatchStore nhưng chưa kịp query từ repository
            if (_matchStore != null)
            {
                var match = _matchStore.GetMatch(matchId);
                if (match != null && match.Phase == RoundPhase.MatchEnded)
                {
                    var saved = await SaveMatchResultAsync(match);
                    if (saved != null)
                    {
                        return new MatchResultResponse
                        {
                            MatchId = saved.MatchId,
                            WinnerId = saved.WinnerId,
                            ParticipantIds = saved.ParticipantIds,
                            TotalRounds = saved.TotalRounds,
                            StartedAt = saved.StartedAt,
                            EndedAt = saved.EndedAt
                        };
                    }
                }
            }

            return null;
        }
    }
}

