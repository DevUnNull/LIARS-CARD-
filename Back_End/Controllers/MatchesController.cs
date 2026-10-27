using System.Security.Claims;
using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/matches")]
    [Authorize]
    public class MatchesController : ControllerBase
    {
        private readonly MatchService _matchService;
        private readonly MatchQueryService _matchQueryService;
        private readonly IMatchResultService _matchResultService;

        public MatchesController(
            MatchService matchService,
            MatchQueryService matchQueryService,
            IMatchResultService matchResultService)
        {
            _matchService = matchService;
            _matchQueryService = matchQueryService;
            _matchResultService = matchResultService;
        }

        private string? GetCurrentPlayerId()
        {
            return User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Tạo hoặc khởi tạo một trận đấu mới với danh sách người chơi (dành cho kiểm thử Swagger/Lobby).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(PublicMatchStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        public ActionResult<PublicMatchStateResponse> CreateMatch([FromBody] CreateMatchRequest request)
        {
            string? currentPlayerId = GetCurrentPlayerId();
            var playerIds = request.PlayerIds ?? new List<string>();

            // Ensure current player is included
            if (!string.IsNullOrEmpty(currentPlayerId) && !playerIds.Contains(currentPlayerId))
            {
                playerIds.Insert(0, currentPlayerId);
            }

            if (playerIds.Count < 2)
            {
                return BadRequest(new GameErrorResponse
                {
                    Code = "INSUFFICIENT_PLAYERS",
                    Message = "Trận đấu cần tối thiểu 2 người chơi."
                });
            }

            var match = _matchService.CreateMatch(request.MatchId, playerIds);
            var state = _matchQueryService.GetPublicState(match.MatchId);
            state.CurrentPlayerId = currentPlayerId;
            return Ok(state);
        }

        /// <summary>
        /// Đánh dấu sẵn sàng (Ready) và tự động bắt đầu vòng chơi khi toàn bộ người chơi sẵn sàng.
        /// </summary>
        [HttpPost("{matchId}/ready")]
        [ProducesResponseType(typeof(PublicMatchStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicMatchStateResponse>> Ready(
            [FromRoute] string matchId,
            [FromBody] ReadyMatchRequest? request)
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

            bool isReady = request?.IsReady ?? true;
            await _matchService.SetReadyAsync(matchId, playerId, isReady);
            var state = await _matchQueryService.GetPublicStateAsync(matchId, playerId);
            return Ok(state);
        }

        /// <summary>
        /// Lấy trạng thái công khai của trận đấu (không để lộ bài của người chơi khác).
        /// </summary>
        [HttpGet("{matchId}/state")]
        [ProducesResponseType(typeof(PublicMatchStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicMatchStateResponse>> GetState([FromRoute] string matchId)
        {
            var playerId = GetCurrentPlayerId();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new GameErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = "Không xác định được danh tính người chơi."
                });
            }

            var state = await _matchQueryService.GetPublicStateAsync(matchId, playerId);
            return Ok(state);
        }

        /// <summary>
        /// Lấy danh sách các lá bài trên tay của người chơi hiện tại từ JWT.
        /// </summary>
        [HttpGet("{matchId}/me/hand")]
        [ProducesResponseType(typeof(PlayerHandResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public ActionResult<PlayerHandResponse> GetMyHand([FromRoute] string matchId)
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

            var hand = _matchQueryService.GetPlayerHand(matchId, playerId);
            return Ok(hand);
        }

        /// <summary>
        /// Lấy kết quả đã lưu của trận đấu nếu người gọi là một người tham gia.
        /// </summary>
        [HttpGet("{matchId}/result")]
        [ProducesResponseType(typeof(MatchResultResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<MatchResultResponse>> GetResult(
            [FromRoute] string matchId,
            CancellationToken cancellationToken)
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

            var result = await _matchResultService.GetForParticipantAsync(
                matchId,
                playerId,
                cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }
    }
}
