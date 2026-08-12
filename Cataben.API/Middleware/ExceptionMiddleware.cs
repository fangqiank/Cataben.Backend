using Cataben.Application.Exceptions;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Cataben.API.Middleware
{
    public class ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env
        )
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = exception switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ValidationException => StatusCodes.Status400BadRequest,
                InsufficientGemsException => StatusCodes.Status400BadRequest,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ExecutionTimeoutException => StatusCodes.Status408RequestTimeout,
                _ => StatusCodes.Status500InternalServerError
            };

            // Domain exceptions carry safe, meaningful messages. For unexpected 5xx errors,
            // avoid leaking internal details in production (stack trace is dev-only anyway).
            var isServerError = context.Response.StatusCode >= 500;
            var response = new
            {
                error = isServerError && !env.IsDevelopment()
                    ? "An internal server error occurred."
                    : exception.Message,
                stackTrace = env.IsDevelopment() ? exception.StackTrace : null,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }

    }

    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
