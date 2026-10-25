using System.Security.Claims;
using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/matches/{matchId}/deck")]
    [Authorize]
    public class DeckActionsController : ControllerBase
    {
        private readonly DeckActionService _deckActionService;
        private readonly ChallengeService _challengeService;

        public DeckActionsController(
            DeckActionService deckActionService,
            ChallengeService challengeService)
        {
            _deckActionService = deckActionService;
            _challengeService = challengeService;
        }

        private string? GetCurrentPlayerId()
        {
            return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Đánh từ 1 đến 3 lá bài từ hand của người chơi. Cho phép đánh bài bluff (nói dối).
        /// </summary>
        [HttpPost("play-cards")]
        [ProducesResponseType(typeof(PlayCardsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PlayCardsResponse>> PlayCards(
            [FromRoute] string matchId,
            [FromBody] PlayCardsRequest request)
        {
            string? playerId = GetCurrentPlayerId();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new GameErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = "Không xác định được danh tính người chơi."
                });
            }

            var response = await _deckActionService.PlayCardsAsync(matchId, playerId, request);
            return Ok(response);
        }

        /// <summary>
        /// Tố cáo lượt đánh gần nhất của đối thủ xem có nói dối (bluff) hay không.
        /// </summary>
        [HttpPost("challenge")]
        [ProducesResponseType(typeof(ChallengeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ChallengeResponse>> Challenge(
            [FromRoute] string matchId,
            [FromBody] ChallengeRequest request)
        {
            string? playerId = GetCurrentPlayerId();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new GameErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = "Không xác định được danh tính người chơi."
                });
            }

            var response = await _challengeService.ChallengeAsync(matchId, playerId, request);
            return Ok(response);
        }
    }
}
