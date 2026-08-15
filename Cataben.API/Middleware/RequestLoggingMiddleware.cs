using System.Diagnostics;

namespace Cataben.API.Middleware
{
    public class RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger
        )
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            logger.LogInformation(
                "Request: {Method} {Path} {QueryString}",
                request.Method,
                request.Path,
                request.QueryString
                );

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();

                logger.LogInformation(
                    "Response: {StatusCode} | Duration: {Duration} ms",
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                    );
            }
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
