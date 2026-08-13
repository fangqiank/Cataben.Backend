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
    public class SubmissionController(
        ISubmissionRepository submissionRepository,
        IMediator mediator,
        ICurrentUserService currentUser
        ) : ControllerBase
    {
        [HttpPost("submit/{challengeId}")]
        [EnableRateLimiting("Execution")]
        [ProducesResponseType(typeof(SubmissionResultDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitChallenge(Guid challengeId, [FromBody] SubmitChallengeRequest request)
        {
            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();

            var command = new SubmitChallengeCommand
            {
                UserId = resolvedUserId.Value,
                ChallengeId = challengeId,
                Code = request.Code,
                UserAgent = Request.Headers.UserAgent.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            var result = await mediator.Send(command);
            // Async pipeline: the submission is queued (code.execute over JetStream); the Worker
            // publishes code.result.{id}, which ExecutionResultReceiver consumes to finalize it.
            // Clients poll GET /submission/{id} for the terminal state.
            return Accepted(result);
        }

        [HttpGet("history")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();
            var userId = resolvedUserId.Value;

            var submissions = await submissionRepository.GetUserSubmissionsAsync(userId, page, pageSize);

            var submissionDtos = submissions.Select(s => new SubmissionDto
            {
                Id = s.Id,
                UserId = s.UserId,
                ChallengeId = s.ChallengeId,
                Code = s.Code.Length > 100 ? s.Code[..100] + "..." : s.Code,
                Status = s.Status,
                IsSuccessful = s.IsSuccessful,
                Score = s.Score,
                TotalScore = s.TotalScore,
                ExecutionTimeMs = s.ExecutionTimeMs,
                MemoryUsedBytes = s.MemoryUsedBytes,
                ErrorMessage = s.ErrorMessage,
                SubmittedAt = s.SubmittedAt,
                CompletedAt = s.CompletedAt,
                ScorePercentage = s.GetScorePercentage(),
                TestResults = s.TestResults.Select(t => new TestResultDto
                {
                    Name = t.Name,
                    Passed = t.Passed,
                    Score = t.Score,
                    Message = t.Message,
                    ExecutionTimeMs = (long)t.ExecutionTime.TotalMilliseconds
                }).ToList()
            });

            return Ok(submissionDtos);
        }

        [HttpGet("{submissionId}")]
        [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubmission(Guid submissionId)
        {
            var submission = await submissionRepository.GetByIdAsync(submissionId);
            if (submission == null) 
                return NotFound();

            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();
            var userId = resolvedUserId.Value;
            if (submission.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            return Ok(new SubmissionDto
            {
                Id = submission.Id,
                UserId = submission.UserId,
                ChallengeId = submission.ChallengeId,
                Code = submission.Code,
                Status = submission.Status,
                IsSuccessful = submission.IsSuccessful,
                Score = submission.Score,
                TotalScore = submission.TotalScore,
                ExecutionTimeMs = submission.ExecutionTimeMs,
                MemoryUsedBytes = submission.MemoryUsedBytes,
                ErrorMessage = submission.ErrorMessage,
                QueryPlan = submission.QueryPlan,
                SubmittedAt = submission.SubmittedAt,
                CompletedAt = submission.CompletedAt,
                ScorePercentage = submission.GetScorePercentage(),
                TestResults = submission.TestResults.Select(t => new TestResultDto
                {
                    Name = t.Name,
                    Passed = t.Passed,
                    Score = t.Score,
                    Message = t.Message,
                    ExecutionTimeMs = (long)t.ExecutionTime.TotalMilliseconds
                }).ToList(),
                StatusHistory = submission.StatusHistory.Select(h => new StatusHistoryDto
                {
                    Status = h.Status,
                    Reason = h.Reason,
                    Timestamp = h.Timestamp
                }).ToList()
            });
        }
    }

    public class SubmitChallengeRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
