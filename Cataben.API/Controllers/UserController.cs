using Cataben.API.Services;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
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
    public class UserController(
        IUserRepository userRepository,
        ISubmissionRepository submissionRepository,
        IXpTransactionRepository xpTransactionRepository,
        IChallengeRepository challengeRepository,
        ILearningPathRepository learningPathRepository,
        ICurrentUserService currentUser
        ) : ControllerBase
    {
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null) 
                return NotFound();

            var submissions = await submissionRepository.GetUserSubmissionsAsync(userId, 1, 1000);
            var successfulSubmissions = submissions.Count(s => s.IsSuccessful);

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Xp = user.Xp,
                Gems = user.Gems,
                Level = user.CalculateLevel(),
                SubmissionsCount = submissions.Count(),
                SuccessfulSubmissions = successfulSubmissions,
                AchievementsCount = user.UserAchievements.Count(a => a.IsCompleted),
                CurrentStreak = CalculateStreak(submissions),
                CreatedAt = user.CreatedAt,
                LastActiveAt = user.LastActiveAt,
                RevealsRemaining = user.RevealsRemaining
            });
        }

        [HttpGet("me/statistics")]
        [ProducesResponseType(typeof(UserStatisticsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCurrentUserStatistics()
        {
            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null) 
                return NotFound();

            var submissions = await submissionRepository.GetUserSubmissionsAsync(userId, 1, 1000);
            var solvedChallenges = submissions
                .Where(s => s.IsSuccessful)
                .Select(s => s.ChallengeId)
                .Distinct()
                .Count();

            var categoryStats = new Dictionary<string, int>();
            var challenges = submissions
                .Where(s => s.IsSuccessful)
                .GroupBy(s => s.Challenge?.Category ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            var totalChallenges = await challengeRepository.GetTotalChallengesAsync();
            var learningPathsCompleted = (await learningPathRepository.GetUserCompletedPathsAsync(userId)).Count();

            return Ok(new UserStatisticsDto
            {
                TotalXp = user.Xp,
                Level = user.CalculateLevel(),
                Gems = user.Gems,
                Submissions = submissions.Count(),
                SolvedChallenges = solvedChallenges,
                TotalChallenges = totalChallenges,
                SuccessRate = submissions.Any()
                ? (double)submissions.Count(s => s.IsSuccessful) / submissions.Count() * 100
                : 0,
                CurrentStreak = CalculateStreak(submissions),
                MaxStreak = CalculateMaxStreak(submissions),
                Achievements = user.UserAchievements.Count(a => a.IsCompleted),
                LearningPathsCompleted = learningPathsCompleted,
                CategoryStats = categoryStats
            });
        }

        [HttpGet("me/activity")]
        [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyActivity([FromQuery] int days = 365)
        {
            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            if (days < 1) days = 1;
            if (days > 730) days = 730;

            // Server-side grouping by UTC day — feeds the activity heatmap. Bucketed by the backend's
            // UTC date, NOT the browser timezone, so cells don't drift a day for non-UTC users.
            var since = DateTime.UtcNow.Date.AddDays(-days);
            var counts = await submissionRepository.GetUserSubmissionCountsByDayAsync(userId, since);

            return Ok(new ActivityDto
            {
                Days = counts.Select(c => new ActivityDayDto { Date = c.Date, Count = c.Count }).ToList()
            });
        }

        private int CalculateMaxStreak(IEnumerable<Submission> submissions)
        {
            var dates = submissions
            .Select(s => s.SubmittedAt.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

            if (!dates.Any()) return 0;

            var maxStreak = 1;
            var currentStreak = 1;
            var expectedDate = dates.First().AddDays(1);

            for (int i = 1; i < dates.Count; i++)
            {
                if (dates[i] == expectedDate)
                {
                    currentStreak++;
                    expectedDate = expectedDate.AddDays(1);
                }
                else
                {
                    maxStreak = Math.Max(maxStreak, currentStreak);
                    currentStreak = 1;
                    expectedDate = dates[i].AddDays(1);
                }
            }

            return Math.Max(maxStreak, currentStreak);
        }

        [HttpGet("me/achievements")]
        [ProducesResponseType(typeof(IEnumerable<UserAchievementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCurrentUserAchievements()
        {
            var userId = await currentUser.GetUserIdAsync() ?? Guid.Empty;
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var achievements = user.UserAchievements.Select(ua => new UserAchievementDto
            {
                Id = ua.Id,
                AchievementId = ua.AchievementId,
                Name = ua.Achievement.Name,
                Description = ua.Achievement.Description,
                Category = ua.Achievement.Category,
                Rarity = ua.Achievement.Rarity,
                Icon = ua.Achievement.Icon,
                BadgeColor = ua.Achievement.BadgeColor,
                Progress = ua.Progress,
                RequiredProgress = ua.Achievement.RequiredProgress,
                IsCompleted = ua.IsCompleted,
                UnlockedAt = ua.UnlockedAt,
                CompletedAt = ua.CompletedAt
            });

            return Ok(achievements);
        }

        [HttpGet("leaderboard")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<LeaderboardDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 50, [FromQuery] string period = "all")
        {
            var leaderboard = new List<LeaderboardDto>();

            // 总榜：按累计 User.Xp 排名（含全部 XP 来源）。
            if (period.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var topUsers = await userRepository.GetTopUsersByXpAsync(limit);
                var solvedCounts = await submissionRepository.GetSolvedCountsAsync(
                    topUsers.Select(u => u.Id));
                var rank = 1;
                foreach (var user in topUsers)
                {
                    leaderboard.Add(new LeaderboardDto
                    {
                        Id = user.Id.ToString(),
                        Name = user.Username,
                        Rank = rank++,
                        Score = user.Xp,
                        Xp = user.Xp,
                        Level = user.CalculateLevel(),
                        SolvedChallenges = solvedCounts.GetValueOrDefault(user.Id),
                        Achievements = user.UserAchievements.Count(a => a.IsCompleted),
                        AvatarUrl = user.AvatarUrl,
                        LastActiveAt = user.LastActiveAt
                    });
                }
                return Ok(leaderboard);
            }

            // 时段榜：按窗口内解题获得 XP（∑ Challenge.XpReward，仅成功提交）排名。
            // Level 仍按累计 XP（稳定、反映整体进度）；Achievements 非时段口径，置 0（UI 不展示）。
            var from = period.ToLowerInvariant() switch
            {
                "last7"  => DateTime.UtcNow.AddDays(-7),
                "last30" => DateTime.UtcNow.AddDays(-30),
                "week"   => StartOfWeekUtc(DateTime.UtcNow),
                "month"  => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _        => DateTime.UtcNow.AddDays(-30) // 未知值兜底为近30天
            };

            var rows = await xpTransactionRepository.GetLeaderboardInPeriodAsync(from, limit);
            {
                var rank = 1;
                foreach (var r in rows)
                {
                    leaderboard.Add(new LeaderboardDto
                    {
                        Id = r.UserId.ToString(),
                        Name = r.Username,
                        Rank = rank++,
                        Score = r.PeriodXp,
                        Xp = r.PeriodXp,
                        Level = Cataben.Domain.Entities.User.ComputeLevel(r.TotalXp),
                        SolvedChallenges = r.PeriodSolved,
                        Achievements = 0,
                        AvatarUrl = r.AvatarUrl,
                        LastActiveAt = r.LastActiveAt
                    });
                }
            }

            return Ok(leaderboard);
        }

        /// <summary>本周一 00:00（UTC，ISO 周一为首日）。用于 leaderboard 的 week 口径。</summary>
        private static DateTime StartOfWeekUtc(DateTime now)
        {
            var date = now.Date;
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return DateTime.SpecifyKind(date.AddDays(-diff), DateTimeKind.Utc);
        }

        private int CalculateStreak(IEnumerable<Submission> submissions)
        {
            var dates = submissions
                .Select(s => s.SubmittedAt.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            if (!dates.Any()) return 0;

            var streak = 0;
            var expectedDate = dates.First();
            foreach (var date in dates)
            {
                if (date == expectedDate)
                {
                    streak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else if (date < expectedDate)
                {
                    break;
                }
            }
            return streak;
        }
    }
}
