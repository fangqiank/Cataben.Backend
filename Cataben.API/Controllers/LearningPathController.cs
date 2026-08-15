using Cataben.API.Services;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("Default")]
    public class LearningPathController(
        ILearningPathRepository learningPathRepository,
        IChallengeRepository challengeRepository,
        ISubmissionRepository submissionRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser
    ) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LearningPathDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLearningPaths()
        {
            var paths = await learningPathRepository.GetAllAsync(onlyPublished: true);

            // 同详情端点：一次查询拿当前用户全部已解题 id，循环内与每路径的题集求交集算进度。
            // 匿名 / 无匹配用户 → 空 solved 集，进度为 0/false（与 GET /{id} 行为一致）。
            var userId = await currentUser.GetUserIdAsync();
            var solvedIds = userId is null
                ? new HashSet<Guid>()
                : (await submissionRepository.GetSolvedChallengeIdsAsync(userId.Value)).ToHashSet();
            var challengeIdsByPath = await challengeRepository
                .GetPublicChallengeIdsByLearningPathAsync(paths.Select(p => p.Id));

            var result = new List<LearningPathDto>();
            foreach (var path in paths)
            {
                var challengeIds = (challengeIdsByPath.TryGetValue(path.Id, out var ids) ? ids : new List<Guid>())
                    .ToHashSet();
                var total = challengeIds.Count;
                var completed = challengeIds.Count(id => solvedIds.Contains(id));

                result.Add(new LearningPathDto
                {
                    Id = path.Id,
                    Name = path.Name,
                    Description = path.Description,
                    Icon = path.Icon,
                    CoverImage = path.CoverImage,
                    Level = path.Level,
                    IsPublished = path.IsPublished,
                    ChallengeCount = total,
                    XpReward = path.XpReward,
                    GemReward = path.GemReward,
                    CreatedAt = path.CreatedAt,
                    PublishedAt = path.PublishedAt,
                    Progress = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0,
                    IsCompleted = total > 0 && completed == total,
                    CompletedChallenges = completed,
                    TotalChallenges = total
                });
            }

            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(LearningPathDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLearningPath(Guid id)
        {
            var path = await learningPathRepository.GetPublishedByIdAsync(id);
            if (path == null) return NotFound();

            var challenges = (await challengeRepository.GetPublicByLearningPathAsync(id)).ToList();

            // Real-time progress from the user's solved challenges — no need for a manual POST to create
            // a UserLearningPath row first. Anonymous / no matching user → empty solved set.
            var userId = await currentUser.GetUserIdAsync();
            var solvedIds = userId is null
                ? new HashSet<Guid>()
                : (await submissionRepository.GetSolvedChallengeIdsAsync(userId.Value)).ToHashSet();

            var challengeDtos = challenges
                .Select(c => new ChallengeBriefDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    Type = c.Type.ToString(),
                    Difficulty = c.Difficulty.Name,
                    XpReward = c.XpReward,
                    IsCompleted = solvedIds.Contains(c.Id)
                })
                .ToList();

            var total = challengeDtos.Count;
            var completed = challengeDtos.Count(c => c.IsCompleted);

            return Ok(new LearningPathDetailDto
            {
                Id = path.Id,
                Name = path.Name,
                Description = path.Description,
                Icon = path.Icon,
                CoverImage = path.CoverImage,
                Level = path.Level,
                IsPublished = path.IsPublished,
                Challenges = challengeDtos,
                Progress = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0,
                IsCompleted = total > 0 && completed == total,
                CompletedChallenges = completed,
                TotalChallenges = total,
                XpReward = path.XpReward,
                GemReward = path.GemReward,
                CreatedAt = path.CreatedAt,
                PublishedAt = path.PublishedAt
            });
        }

        [HttpPost("{id}/progress")]
        [Authorize]
        public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] UpdateProgressRequest request)
        {
            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();
            var userId = resolvedUserId.Value;
            var path = await learningPathRepository.GetPublishedByIdAsync(id);
            if (path == null) return NotFound();

            var progress = await learningPathRepository.GetUserProgressAsync(userId, id);
            if (progress == null)
            {
                // GetByIdAsync returns User? — the row could be gone between the CurrentUserService
                // lookup and now; guard it rather than passing null to the UserLearningPath ctor.
                var user = await userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound();
                progress = new UserLearningPath(user, path);
            }

            progress.UpdateProgress(request.CompletedChallenges);
            await learningPathRepository.UpdateUserProgressAsync(progress);

            return Ok(new { progress = progress.Progress, isCompleted = progress.IsCompleted });
        }

    }

    public class UpdateProgressRequest
    {
        public int CompletedChallenges { get; set; }
    }
}
