using Cataben.API.Services;
using Cataben.Application.DTOs;
using Cataben.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("Default")]
    public class AchievementController(
        IAchievementService achievementService,
        ICurrentUserService currentUser
        ) : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<AchievementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllAchievements()
        {
            var achievements = await achievementService.GetAllAchievementsAsync();
            return Ok(achievements);
        }

        [HttpGet("user")]
        [ProducesResponseType(typeof(IEnumerable<UserAchievementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserAchievements()
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var achievements = await achievementService.GetUserAchievementsAsync(userId.Value);
            return Ok(achievements);
        }

        [HttpGet("user/unlocked")]
        [ProducesResponseType(typeof(IEnumerable<UserAchievementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUnlockedAchievements()
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var achievements = await achievementService.GetUnlockedAchievementsAsync(userId.Value);
            return Ok(achievements);
        }

        [HttpGet("user/stats")]
        [ProducesResponseType(typeof(AchievementStatisticsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAchievementStatistics()
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var stats = await achievementService.GetAchievementStatisticsAsync(userId.Value);
            return Ok(stats);
        }

        [HttpGet("{achievementId}/progress")]
        [ProducesResponseType(typeof(AchievementProgressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAchievementProgress(string achievementId)
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var progress = await achievementService.GetAchievementProgressAsync(userId.Value, achievementId);
            if (progress == null) return NotFound();
            return Ok(progress);
        }

        [HttpGet("leaderboard")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<AchievementLeaderboardDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 50)
        {
            var leaderboard = await achievementService.GetAchievementLeaderboardAsync(limit);
            return Ok(leaderboard);
        }

    }
}
