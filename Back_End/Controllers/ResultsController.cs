using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/matches/{matchId}/result")]
    [Authorize]
    public class ResultsController : ControllerBase
    {
        private readonly MatchResultService _matchResultService;
        private readonly IMatchStore _matchStore;

        public ResultsController(MatchResultService matchResultService, IMatchStore matchStore)
        {
            _matchResultService = matchResultService;
            _matchStore = matchStore;
        }

        /// <summary>
        /// Lấy kết quả cuối cùng của trận đấu sau khi đã hoàn thành.
        /// Client không thể tự gửi kết quả; nghiệp vụ do server tự động lưu trữ.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(MatchResultResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(GameErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MatchResultResponse>> GetMatchResult([FromRoute] string matchId)
        {
            var match = _matchStore.GetMatch(matchId);
            if (match != null && match.Phase != RoundPhase.MatchEnded)
            {
                return BadRequest(new GameErrorResponse
                {
                    Code = "MATCH_NOT_FINISHED",
                    Message = "Trận đấu vẫn đang diễn ra, chưa có kết quả cuối cùng.",
                    CurrentVersion = match.StateVersion
                });
            }

            var result = await _matchResultService.GetMatchResultAsync(matchId);
            if (result == null)
            {
                return NotFound(new GameErrorResponse
                {
                    Code = "MATCH_RESULT_NOT_FOUND",
                    Message = "Không tìm thấy kết quả của trận đấu này."
                });
            }

            return Ok(result);
        }
    }
}

