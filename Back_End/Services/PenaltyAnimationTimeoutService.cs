using Back_End.Infrastructure.MatchState;

namespace Back_End.Services
{
    public sealed class PenaltyAnimationTimeoutService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(250);
        private readonly IMatchStore _matchStore;
        private readonly IPenaltyAnimationService _penaltyAnimationService;
        private readonly ILogger<PenaltyAnimationTimeoutService> _logger;

        public PenaltyAnimationTimeoutService(
            IMatchStore matchStore,
            IPenaltyAnimationService penaltyAnimationService,
            ILogger<PenaltyAnimationTimeoutService> logger)
        {
            _matchStore = matchStore;
            _penaltyAnimationService = penaltyAnimationService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ScanInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var (matchId, penaltyEventId) in _matchStore.GetPendingPenaltyAnimationIds())
                {
                    try
                    {
                        await _penaltyAnimationService.TryTimeoutAsync(
                            matchId,
                            penaltyEventId,
                            stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to check penalty animation timeout for {MatchId}/{PenaltyEventId}.",
                            matchId,
                            penaltyEventId);
                    }
                }
            }
        }
    }
}
