using Back_End.DTOs;
using Back_End.Exceptions;
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
        }
    }
}
