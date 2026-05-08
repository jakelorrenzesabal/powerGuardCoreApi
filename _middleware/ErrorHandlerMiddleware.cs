using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi._middleware
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Unhandled exception");

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = err switch
                {
                    AppException => (int)HttpStatusCode.BadRequest,
                    RoomInactiveException => (int)HttpStatusCode.BadRequest,
                    DeviceOfflineException => (int)HttpStatusCode.ServiceUnavailable,
                    UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                    _ when err.Message.ToLower().Contains("not found") => (int)HttpStatusCode.NotFound,
                    _ when err.Message.ToLower().Contains("deactivated") => (int)HttpStatusCode.Forbidden,
                    ArgumentException => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                var result = JsonSerializer.Serialize(new { message = err.Message });
                await context.Response.WriteAsync(result);
            }
        }
    }
}