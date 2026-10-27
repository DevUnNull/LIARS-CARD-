using Back_End.DTOs;

namespace Back_End.Services
{
    public interface IGameplayStateNotifier
    {
        Task PublishPlayerEliminatedAsync(
            PlayerEliminatedEvent message,
            CancellationToken cancellationToken);

        Task PublishMatchEndedAsync(
            MatchEndedEvent message,
            CancellationToken cancellationToken);

        Task PublishRoundStartedAsync(
            RoundStartedEvent message,
            CancellationToken cancellationToken);
    }
}
