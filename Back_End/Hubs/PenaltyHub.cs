using System.Security.Claims;
using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Back_End.Hubs
{
    [Authorize]
    public sealed class PenaltyHub : Hub
    {
        private readonly IPenaltyAnimationService _penaltyAnimationService;

        public PenaltyHub(IPenaltyAnimationService penaltyAnimationService)
        {
            _penaltyAnimationService = penaltyAnimationService;
        }

        public static string GetMatchGroupName(string matchId) => $"match:{matchId}";

        public async Task<IReadOnlyList<PenaltyAnimationSnapshot>> JoinMatch(string matchId)
        {
            string playerId = GetPlayerId();

            await _penaltyAnimationService.GetReconnectSnapshotAsync(
                matchId,
                playerId,
                Context.ConnectionAborted);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetMatchGroupName(matchId),
                Context.ConnectionAborted);

            try
            {
                return await _penaltyAnimationService.GetReconnectSnapshotAsync(
                    matchId,
                    playerId,
                    Context.ConnectionAborted);
            }
            catch
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    GetMatchGroupName(matchId),
                    CancellationToken.None);
                throw;
            }
        }

        public Task<PenaltyAnimationAckResponse> AckPenaltyAnimation(
            string matchId,
            Guid penaltyEventId)
        {
            return _penaltyAnimationService.AcknowledgeAsync(
                matchId,
                penaltyEventId,
                GetPlayerId(),
                Context.ConnectionAborted);
        }

        private string GetPlayerId()
        {
            var playerId = Context.User?.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new HubException("Authenticated player ID is missing.");
            }

            return playerId;
        }
    }
}
