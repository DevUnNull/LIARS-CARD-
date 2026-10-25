using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/connection")]
    [Authorize]
    public class ConnectionController : ControllerBase
    {
        private ConnectionTicketService ticketService;

        public ConnectionController(ConnectionTicketService ticketService)
        {
            this.ticketService = ticketService;
        }

        [HttpPost("ticket")]
        public IActionResult CreateTicket()
        {
            string? playerId = User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Unauthorized(new
                {
                    message = "Token không có Player ID."
                });
            }

            string ticket = ticketService.Create(playerId);

            Response.Headers["Cache-Control"] = "no-store";

            return Ok(new
            {
                ticket,
                expiresIn = ConnectionTicketService.LIFE_TIME
            });
        }
    }
}
