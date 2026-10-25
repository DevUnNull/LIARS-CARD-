using System;
using System.Collections.Generic;
using System.Security.Claims;
using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/tables")]
    [Authorize]
    public class TablesController : ControllerBase
    {
        private readonly TableService _tableService;
        private readonly MatchService _matchService;

        public TablesController(TableService tableService, MatchService matchService)
        {
            _tableService = tableService;
            _matchService = matchService;
        }

        private string? GetCurrentPlayerId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        }

        /// <summary>
        /// Lấy danh sách toàn bộ bàn chơi trong sảnh.
        /// </summary>
        [HttpGet]
        public ActionResult<List<TableResponse>> GetTables()
        {
            return Ok(_tableService.GetTables());
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một bàn chơi (ghế, chủ bàn, người chơi, ván đấu hiện tại).
        /// </summary>
        [HttpGet("{id:int}")]
        public ActionResult<TableResponse> GetTableByID(int id)
        {
            TableResponse? table = _tableService.GetTableByID(id);

            if (table == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy bàn."
                });
            }

            return Ok(table);
        }

        /// <summary>
        /// Ngồi vào ghế của bàn chơi.
        /// </summary>
        [HttpPost("{tableId:int}/sit")]
        public IActionResult SitDown([FromRoute] int tableId, [FromBody] SitDownRequest request)
        {
            string? playerId = GetCurrentPlayerId();

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    message = "Không xác định được người chơi."
                });
            }

            if (!request.SeatIndex.HasValue)
            {
                return BadRequest(new
                {
                    message = "Vui lòng chọn ghế."
                });
            }

            string? error = _tableService.SitDown(tableId, request.SeatIndex.Value, playerId);

            if (error == "TABLE_NOT_FOUND")
            {
                return NotFound(new { code = error });
            }

            if (error == "INVALID_SEAT")
            {
                return BadRequest(new { code = error });
            }

            if (error != null)
            {
                return Conflict(new { code = error });
            }

            return Ok(new
            {
                message = "Ngồi ghế thành công.",
                tableId,
                seatIndex = request.SeatIndex.Value
            });
        }

        /// <summary>
        /// Rời ghế hiện tại của người chơi.
        /// </summary>
        [HttpPost("leaveSeat")]
        public IActionResult LeaveSeat()
        {
            string? playerId = GetCurrentPlayerId();

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    message = "Không xác định được người chơi."
                });
            }

            string? error = _tableService.LeaveSeat(playerId);

            if (error != null)
            {
                return Conflict(new { code = error });
            }

            return Ok(new
            {
                message = "Rời ghế thành công."
            });
        }

        /// <summary>
        /// Đổi trạng thái sẵn sàng (Ready) tại bàn chờ.
        /// </summary>
        [HttpPost("ready")]
        public IActionResult SetReady([FromBody] SetReadyRequest readyRequest)
        {
            string? playerId = GetCurrentPlayerId();

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    message = "Không xác định được người chơi."
                });
            }

            if (!readyRequest.Ready.HasValue)
            {
                return BadRequest(new
                {
                    message = "Vui lòng gửi trạng thái ready."
                });
            }

            string? error = _tableService.SetReady(
                playerId,
                readyRequest.Ready.Value
            );

            if (error != null)
            {
                return Conflict(new { code = error });
            }

            return Ok(new
            {
                message = "Cập nhật trạng thái sẵn sàng thành công.",
                ready = readyRequest.Ready.Value
            });
        }

        /// <summary>
        /// Chủ bàn bấm Bắt đầu trận đấu (Phương án B) khi có tối thiểu 2 người và mọi người đã sẵn sàng.
        /// </summary>
        [HttpPost("{tableId:int}/start-match")]
        [ProducesResponseType(typeof(StartMatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public IActionResult StartMatch([FromRoute] int tableId)
        {
            string? playerId = GetCurrentPlayerId();

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    code = "UNAUTHORIZED",
                    message = "Không xác định được danh tính người chơi."
                });
            }

            var (error, playerIds) = _tableService.ValidateAndStartMatch(tableId, playerId);

            if (error == "TABLE_NOT_FOUND")
            {
                return NotFound(new { code = error, message = "Không tìm thấy bàn." });
            }

            if (error == "TABLE_NOT_WAITING")
            {
                return Conflict(new { code = error, message = "Bàn không ở trạng thái chờ." });
            }

            if (error == "NOT_TABLE_HOST")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = error, message = "Chỉ chủ bàn mới có quyền bắt đầu trận đấu." });
            }

            if (error == "INSUFFICIENT_PLAYERS")
            {
                return BadRequest(new { code = error, message = "Cần tối thiểu 2 người chơi để bắt đầu trận đấu." });
            }

            if (error == "TABLE_NOT_READY")
            {
                return Conflict(new { code = error, message = "Vẫn còn người chơi trong bàn chưa sẵn sàng." });
            }

            if (error != null)
            {
                return Conflict(new { code = error });
            }

            // Sinh mã trận đấu duy nhất
            string matchId = $"m_table_{tableId}_{Guid.NewGuid().ToString("N")[..8]}";

            // Khởi tạo trận đấu bài
            _matchService.CreateMatch(matchId, playerIds!);

            // Gán MatchId vào bàn để các client khác đọc được qua GET /api/tables/{id}
            _tableService.SetCurrentMatch(tableId, matchId);

            return Ok(new StartMatchResponse
            {
                TableId = tableId,
                MatchId = matchId,
                Message = "Bắt đầu trận đấu thành công.",
                PlayerIds = playerIds!
            });
        }
    }
}
