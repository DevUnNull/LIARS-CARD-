using Back_End.Models;

namespace Back_End.Data
{
    public interface IMatchResultRepository
    {
        Task EnsureIndexesAsync(CancellationToken cancellationToken);
        Task<bool> TryInsertAsync(
            MatchResultDocument document,
            CancellationToken cancellationToken);
        Task<MatchResultDocument?> GetByMatchIdAsync(
            string matchId,
            CancellationToken cancellationToken);
    }
}
