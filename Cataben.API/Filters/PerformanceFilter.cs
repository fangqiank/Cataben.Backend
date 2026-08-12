using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace Cataben.API.Filters
{
    public class PerformanceFilter(ILogger<PerformanceFilter> logger) : IActionFilter
    {
        private Stopwatch _stopwatch = null!;
        public void OnActionExecuted(ActionExecutedContext context)
        {
            _stopwatch.Stop();
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            var action = context.ActionDescriptor.DisplayName;
            var statusCode = context.HttpContext.Response.StatusCode;

            if(elapsedMs > 1000) 
                logger.LogWarning(
                    "Slow request: {Action} took {Elapsed}ms with status {StatusCode}",
                    action,
                    elapsedMs,
                    statusCode);
            else
                logger.LogInformation(
                    "Request completed: {Action} took {Elapsed}ms with status {StatusCode}",
                    action,
                    elapsedMs,
                    statusCode);
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            _stopwatch = Stopwatch.StartNew();
        }
    }
}
