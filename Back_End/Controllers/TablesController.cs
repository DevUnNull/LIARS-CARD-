using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/tables")]
    [Authorize]
    public class TablesController : ControllerBase
    {
        private readonly TableService tableService;

        public TablesController(TableService tableService)
        {
            this.tableService = tableService;
        }

        [HttpGet]
        public ActionResult<List<TableResponse>> GetTables()
        {
            return Ok(tableService.GetTables());
        }

        [HttpGet("{id:int}")]
        public ActionResult<TableResponse> GetTableByID(int id)
        {
            TableResponse? table = tableService.GetTableByID(id);

            if (table == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy bàn."
                });
            }

            return Ok(table);
        }

        [HttpPost("{tableId:int}/sit")]
        public IActionResult SitDown([FromRoute] int tableId, [FromBody] SitDownRequest request)
        {
            string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

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

            string? error = tableService.SitDown(tableId, request.SeatIndex.Value, playerId);

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

        [HttpPost("leaveSeat")]
        public IActionResult LeaveSeat()
        {
            string? playerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    message = "Không xác định được người chơi."
                });
            }

            string? error = tableService.LeaveSeat(playerId);

            if (error != null)
            {
                return Conflict(new { code = error });
            }

            return Ok(new
            {
                message = "Rời ghế thành công."
            });
        }

        [HttpPost("ready")]
        public IActionResult SetReady([FromBody] SetReadyRequest readyRequest)
        {
            string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

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

            string? error = tableService.SetReady(
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
    }
}
