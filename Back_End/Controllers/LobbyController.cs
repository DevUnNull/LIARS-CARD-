using Back_End.DTOs;
using Back_End.Models;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/lobbies")]
    [Authorize]
    public class LobbyController : ControllerBase
    {
        private readonly LobbyStore _lobbyStore;

        public LobbyController(LobbyStore lobbyStore)
        {
            _lobbyStore = lobbyStore;
        }

        [HttpPost]
        public IActionResult CreateLobby(
            [FromBody] CreateLobbyRequest request)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            ) ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var lobby = new Lobby
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name.Trim(),
                HostId = userId,
                MaxPlayers = request.MaxPlayers,
                PlayerIds = new List<string> { userId },
                IsInGame = false,
                CreatedAt = DateTime.UtcNow
            };

            if (!_lobbyStore.Lobbies.TryAdd(lobby.Id, lobby))
            {
                return Conflict(new
                {
                    message = "Không thể tạo phòng"
                });
            }

            return StatusCode(201, new
            {
                message = "Tạo phòng thành công",
                lobbyId = lobby.Id,
                name = lobby.Name,
                hostId = lobby.HostId,
                maxPlayers = lobby.MaxPlayers,
                playerCount = lobby.PlayerIds.Count
            });
        }
    }
}
