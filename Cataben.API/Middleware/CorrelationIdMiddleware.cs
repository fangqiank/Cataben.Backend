namespace Cataben.API.Middleware
{
    public class CorrelationIdMiddleware(
        RequestDelegate next, 
        ILogger<CorrelationIdMiddleware> logger
        )
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"]
                .FirstOrDefault();

            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            context.TraceIdentifier = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            using(logger.BeginScope(new Dictionary<string, object> 
            { 
                ["CorrelationId"] = correlationId 
            }))
            {
                await next(context);
            }
        }
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}
