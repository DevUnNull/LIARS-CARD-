using Back_End.DTOs;

namespace Back_End.Services
{
    public interface IPenaltyAnimationNotifier
    {
        Task PublishStartedAsync(PenaltyAnimationStarted message, CancellationToken cancellationToken);
        Task PublishEndedAsync(PenaltyAnimationEnded message, CancellationToken cancellationToken);
    }
}
