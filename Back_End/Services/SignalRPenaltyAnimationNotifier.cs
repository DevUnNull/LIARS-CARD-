using Back_End.DTOs;
using Back_End.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Back_End.Services
{
    public sealed class SignalRPenaltyAnimationNotifier : IPenaltyAnimationNotifier
    {
        private readonly IHubContext<PenaltyHub> _hubContext;

        public SignalRPenaltyAnimationNotifier(IHubContext<PenaltyHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task PublishStartedAsync(
            PenaltyAnimationStarted message,
            CancellationToken cancellationToken)
        {
            return _hubContext.Clients
                .Group(PenaltyHub.GetMatchGroupName(message.MatchId))
                .SendAsync("PenaltyAnimationStarted", message, cancellationToken);
        }

        public Task PublishEndedAsync(
            PenaltyAnimationEnded message,
            CancellationToken cancellationToken)
        {
            return _hubContext.Clients
                .Group(PenaltyHub.GetMatchGroupName(message.MatchId))
                .SendAsync("PenaltyAnimationEnded", message, cancellationToken);
        }
    }
}
