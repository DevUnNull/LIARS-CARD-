using Back_End.DTOs;
using Back_End.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Back_End.Filters
{
    public class GameExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is GameRuleException gameEx)
            {
                var errorResponse = new GameErrorResponse
                {
                    Code = gameEx.Code,
                    Message = gameEx.Message,
                    CurrentVersion = gameEx.CurrentVersion
                };

                context.Result = new ObjectResult(errorResponse)
                {
                    StatusCode = (int)gameEx.StatusCode
                };

                context.ExceptionHandled = true;
            }
            else if (context.Exception is MatchResultPersistenceException)
            {
                context.Result = new ObjectResult(new GameErrorResponse
                {
                    Code = "MATCH_RESULT_UNAVAILABLE",
                    Message = "Kết quả trận đấu hiện chưa thể được lưu hoặc truy vấn."
                })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                context.ExceptionHandled = true;
            }
            else if (context.Exception is MatchResultConflictException)
            {
                context.Result = new ObjectResult(new GameErrorResponse
                {
                    Code = "MATCH_RESULT_CONFLICT",
                    Message = "Kết quả đã lưu của trận đấu không khớp với trạng thái cuối hiện tại."
                })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                context.ExceptionHandled = true;
            }
        }
    }
}
