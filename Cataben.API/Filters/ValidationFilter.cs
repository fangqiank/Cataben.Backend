using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cataben.API.Filters
{
    public class ValidationFilter(ILogger<ValidationFilter> logger) : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if(!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    );
                logger.LogWarning("Validation failed for {Action}", context.ActionDescriptor.DisplayName);
                
                context.Result = new BadRequestObjectResult(new
                {
                    Errors = errors,
                    message = "Validation failed",
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
