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

        public MatchesController(MatchService matchService, MatchQueryService matchQueryService)
        {
            _matchService = matchService;
            _matchQueryService = matchQueryService;
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
            return Ok(state);
        }

        /// <summary>
        /// Tham gia vào trận đấu Authoritative Server bằng Join Ticket cấp từ bàn chơi (Handoff).
        /// </summary>
        [HttpPost("{matchId}/join")]
        [ProducesResponseType(typeof(PublicMatchStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicMatchStateResponse>> JoinMatch(
            [FromRoute] string matchId,
            [FromBody] JoinMatchRequest request)
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

            await _matchService.JoinMatchAsync(matchId, playerId, request.JoinTicket);
            var state = _matchQueryService.GetPublicState(matchId);
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
            var state = _matchQueryService.GetPublicState(matchId);
            return Ok(state);
        }

        /// <summary>
        /// Lấy trạng thái công khai của trận đấu (không để lộ bài của người chơi khác).
        /// </summary>
        [HttpGet("{matchId}/state")]
        [ProducesResponseType(typeof(PublicMatchStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public ActionResult<PublicMatchStateResponse> GetState([FromRoute] string matchId)
        {
            var state = _matchQueryService.GetPublicState(matchId);
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
        /// Xác nhận đã hoàn thành animation xử phạt (ACK). Server kiểm tra và chuyển tiếp vòng đấu hoặc kết thúc trận.
        /// </summary>
        [HttpPost("{matchId}/penalty/ack")]
        [ProducesResponseType(typeof(PenaltyAckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PenaltyAckResponse>> AcknowledgePenalty(
            [FromRoute] string matchId,
            [FromBody] PenaltyAckRequest request)
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

            var response = await _matchService.AcknowledgePenaltyAsync(matchId, playerId, request);
            return Ok(response);
        }
    }
}
