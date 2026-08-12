using Cataben.API.Services;
using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("Default")]
    public class ChallengeController(
        IChallengeRepository challengeRepository,
        ISubmissionRepository submissionRepository,
        ICurrentUserService currentUser,
        IMediator mediator,
        ILogger<ChallengeController> logger
        ) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ChallengeDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChallenges(
            [FromQuery] string? category,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            // Fetch the user's solved challenge ids once (avoids an N+1 query per challenge).
            var solvedIds = (await submissionRepository.GetSolvedChallengeIdsAsync(userId)).ToHashSet();
            var challenges = await challengeRepository.GetAllAsync(null, category, page, pageSize);

            var challengeDtos = challenges.Select(c => MapChallenge(c, solvedIds.Contains(c.Id))).ToList();
            return Ok(challengeDtos);
        }

        [HttpGet("daily")]
        [ProducesResponseType(typeof(DailyChallengeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDailyChallenge()
        {
            var active = (await challengeRepository.GetAllActiveAsync()).ToList();
            if (active.Count == 0)
                return NotFound();

            // Deterministic pick — integer seed, NOT string.GetHashCode() (which is randomized per-process
            // and would change the featured challenge across restarts / instances).
            var today = DateTime.UtcNow.Date;
            var seed = today.Year * 10000 + today.Month * 100 + today.Day;
            var challenge = active[Math.Abs(seed) % active.Count];

            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            var isSolved = await submissionRepository.GetUserSubmissionForChallenge(userId, challenge.Id) > 0;

            return Ok(new DailyChallengeDto
            {
                Date = today,
                Challenge = MapChallenge(challenge, isSolved)
            });
        }

        // {id:guid} so the literal "daily" route above is never captured as an id parameter.
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ChallengeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChallenge(Guid id)
        {
            var challenge = await challengeRepository.GetByIdAsync(id);
            if (challenge == null)
                return NotFound();

            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            var isSolved = await submissionRepository.GetUserSubmissionForChallenge(userId, id) > 0;

            return Ok(MapChallenge(challenge, isSolved));
        }

        // Consumes one of the user's global reveal credits and returns the reference solution.
        // The rest of this controller is public; reveal is personal, so this action is [Authorize].
        // Credit exhaustion → ValidationException → 400 (mapped by ExceptionMiddleware).
        [HttpPost("{id:guid}/reveal")]
        [Authorize]
        [ProducesResponseType(typeof(RevealSolutionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevealSolution(Guid id)
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return Unauthorized();

            var result = await mediator.Send(new RevealSolutionCommand
            {
                UserId = userId.Value,
                ChallengeId = id
            });
            return Ok(result);
        }

        /// <summary>Maps a Challenge entity to its DTO, exposing only public test cases.</summary>
        private static ChallengeDto MapChallenge(Challenge challenge, bool isSolved)
        {
            return new ChallengeDto
            {
                Id = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                Type = challenge.Type,
                Difficulty = challenge.Difficulty.Name,
                Category = challenge.Category,
                InitialCode = challenge.InitialCode,
                XpReward = challenge.XpReward,
                GemReward = challenge.GemReward,
                Hints = challenge.Hints.ToList(),
                TimeLimitSeconds = challenge.TimeLimitSeconds,
                MemoryLimitMb = challenge.MemoryLimitMb,
                SuccessRate = 0,
                TotalSubmissions = 0,
                IsSolved = isSolved,
                TestCases = challenge.TestCases
                    .Where(t => t.IsPublic)
                    .Select(t => new TestCaseDto
                    {
                        Name = t.Name,
                        Input = t.Input,
                        ExpectedOutput = t.ExpectedOutput,
                        IsPublic = t.IsPublic,
                        Weight = t.Weight
                    })
                    .ToList()
            };
        }
    }
}
