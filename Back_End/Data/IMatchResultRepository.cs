using Back_End.Models;

namespace Back_End.Data
{
    public interface IMatchResultRepository
    {
        Task<bool> SaveAsync(MatchResult result);
        Task<MatchResult?> GetByMatchIdAsync(string matchId);
    }
}

