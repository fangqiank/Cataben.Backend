using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class UserRepository(
        AppDbContext context
        ) : IUserRepository
    {
        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .Include(u => u.Submissions)
                .Include(u => u.UserAchievements)
                    .ThenInclude(a => a.Achievement)
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByIdBasicAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
        }

        public async Task<IEnumerable<User>> GetAllAsync(
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            return await context.Users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
        {
            return await context.Users.CountAsync(cancellationToken);
        }

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            await context.Users.AddAsync(user, cancellationToken);
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            context.Users.Update(user);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var user = context.Users.Find(id);
            if (user != null)
                context.Users.Remove(user);

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<User>> GetTopUsersByXpAsync(int count, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .OrderByDescending(u => u.Xp)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetUserRankAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null)
                return 0;

            return await context.Users.CountAsync(u => u.Xp > user.Xp, cancellationToken) + 1;
        }

        public async Task<int> GetCompletedAchievementCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .CountAsync(ua => ua.UserId == userId && ua.IsCompleted, cancellationToken);
        }

        public async Task<IEnumerable<UserAchievement>> GetUserCompletedAchievementsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId && ua.IsCompleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserAchievementStats> GetUserAchievementStatsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var achievements = await context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId)
                .ToListAsync(cancellationToken);

            return new UserAchievementStats
            {
                Total = achievements.Count,
                Completed = achievements.Count(a => a.IsCompleted),
                InProgress = achievements.Count(a => !a.IsCompleted && a.Progress > 0),
                TotalXpEarned = achievements.Where(a => a.IsCompleted).Sum(a => a.Achievement.XpReward),
                TotalGemsEarned = achievements.Where(a => a.IsCompleted).Sum(a => a.Achievement.GemReward),
                ByRarity = achievements
                    .Where(a => a.IsCompleted)
                    .GroupBy(a => a.Achievement.Rarity)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count())
            };
        }
    }
}
