using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class AchievementRepository(AppDbContext context) : IAchievementRepository
    {
        public async Task<IEnumerable<Achievement>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await context.Achievements
                .OrderBy(a => a.Order)
                .ToListAsync(cancellationToken);
        }

        public async Task<Achievement?> GetByIdAsync(string achievementId, CancellationToken cancellationToken = default)
        {
            return await context.Achievements.FirstOrDefaultAsync(a => a.Id == achievementId, cancellationToken);
        }

        public async Task AddAsync(Achievement achievement, CancellationToken cancellationToken = default)
        {
            await context.Achievements.AddAsync(achievement, cancellationToken);
        }

        public async Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserAchievement?> GetUserAchievementAsync(
            Guid userId,
            string achievementId,
            CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .Include(ua => ua.Achievement)
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.AchievementId == achievementId, cancellationToken);
        }

        public async Task<HashSet<string>> GetUnlockedAchievementIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var ids = await context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.IsCompleted)
                .Select(ua => ua.AchievementId)
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        public async Task AddUserAchievementAsync(
            UserAchievement userAchievement,
            CancellationToken cancellationToken = default)
        {
            await context.UserAchievements.AddAsync(userAchievement, cancellationToken);
        }

        public Task UpdateUserAchievementAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default)
        {
            context.UserAchievements.Update(userAchievement);
            return Task.CompletedTask;
        }

        public async Task<int> GetMentorCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.AchievementId == "mentor" && ua.IsCompleted)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetUserRankAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var score = await context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.IsCompleted)
                .SumAsync(ua =>
                    ua.Achievement.Rarity == AchievementRarity.Common ? 1 :
                    ua.Achievement.Rarity == AchievementRarity.Uncommon ? 2 :
                    ua.Achievement.Rarity == AchievementRarity.Rare ? 4 :
                    ua.Achievement.Rarity == AchievementRarity.Epic ? 8 :
                    ua.Achievement.Rarity == AchievementRarity.Legendary ? 16 :
                    ua.Achievement.Rarity == AchievementRarity.Mythic ? 32 : 0, cancellationToken);

            return await context.UserAchievements
                .Where(ua => ua.IsCompleted)
                .GroupBy(ua => ua.UserId)
                .Select(g => g.Sum(ua =>
                    ua.Achievement.Rarity == AchievementRarity.Common ? 1 :
                    ua.Achievement.Rarity == AchievementRarity.Uncommon ? 2 :
                    ua.Achievement.Rarity == AchievementRarity.Rare ? 4 :
                    ua.Achievement.Rarity == AchievementRarity.Epic ? 8 :
                    ua.Achievement.Rarity == AchievementRarity.Legendary ? 16 :
                    ua.Achievement.Rarity == AchievementRarity.Mythic ? 32 : 0))
                .CountAsync(s => s > score, cancellationToken) + 1;
        }

        public async Task<IEnumerable<AchievementLeaderboardDto>> GetAchievementLeaderboardAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .Where(ua => ua.IsCompleted)
                .GroupBy(ua => new { ua.UserId, ua.User.Username })
                .Select(g => new AchievementLeaderboardDto
                {
                    UserId = g.Key.UserId,
                    Username = g.Key.Username,
                    Score = g.Sum(ua =>
                        ua.Achievement.Rarity == AchievementRarity.Common ? 1 :
                        ua.Achievement.Rarity == AchievementRarity.Uncommon ? 2 :
                        ua.Achievement.Rarity == AchievementRarity.Rare ? 4 :
                        ua.Achievement.Rarity == AchievementRarity.Epic ? 8 :
                        ua.Achievement.Rarity == AchievementRarity.Legendary ? 16 :
                        ua.Achievement.Rarity == AchievementRarity.Mythic ? 32 : 0),
                    AchievementsCount = g.Count()
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
