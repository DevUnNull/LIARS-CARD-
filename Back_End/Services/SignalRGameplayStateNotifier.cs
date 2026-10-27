using Back_End.DTOs;
using Back_End.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Back_End.Services
{
    public sealed class SignalRGameplayStateNotifier : IGameplayStateNotifier
    {
        private readonly IHubContext<PenaltyHub> _hubContext;
        private readonly ILogger<SignalRGameplayStateNotifier> _logger;

        public SignalRGameplayStateNotifier(
            IHubContext<PenaltyHub> hubContext,
            ILogger<SignalRGameplayStateNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public Task PublishPlayerEliminatedAsync(
            PlayerEliminatedEvent message,
            CancellationToken cancellationToken) =>
            PublishAsync(
                message.MatchId,
                "PlayerEliminated",
                message,
                message.StateVersion,
                cancellationToken);

        public Task PublishMatchEndedAsync(
            MatchEndedEvent message,
            CancellationToken cancellationToken) =>
            PublishAsync(
                message.MatchId,
                "MatchEnded",
                message,
                message.StateVersion,
                cancellationToken);

        public Task PublishRoundStartedAsync(
            RoundStartedEvent message,
            CancellationToken cancellationToken) =>
            PublishAsync(
                message.MatchId,
                "RoundStarted",
                message,
                message.StateVersion,
                cancellationToken);

        private async Task PublishAsync<T>(
            string matchId,
            string eventName,
            T message,
            int stateVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients
                    .Group(PenaltyHub.GetMatchGroupName(matchId))
                    .SendAsync(eventName, message, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to publish {EventName} for match {MatchId} at state version {StateVersion}.",
                    eventName,
                    matchId,
                    stateVersion);
            }
        }
    }
}
