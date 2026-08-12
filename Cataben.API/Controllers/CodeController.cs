using Cataben.API.Services;
using Cataben.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("Default")]
    public class CodeController(
        IMediator mediator,
        ICurrentUserService currentUser,
        ILogger<CodeController> logger
        ) : ControllerBase
    {
        [HttpPost("execute")]
        [EnableRateLimiting("Execution")]
        [ProducesResponseType(typeof(ExecutionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Execute([FromBody] CodeExecutionRequest request)
        {
            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();

            var command = new ExecuteCodeCommand
            {
                UserId = resolvedUserId.Value,
                Code = request.Code,
                ChallengeId = request.ChallengeId,
                Type = request.Type,
                Parameters = request.Parameters ?? new(),
                IsSubmission = request.IsSubmission,
                UserAgent = Request.Headers.UserAgent.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            var result = await mediator.Send(command);
            return Ok(result);
        }
    }
}
