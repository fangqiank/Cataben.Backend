using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Services
{
    public class AchievementService(
        IAchievementRepository achievementRepository,
        IUserRepository userRepository,
        IXpTransactionRepository xpTransactionRepository,
        ICacheService cache,
        ILogger<AchievementService> logger)
        : IAchievementService
    {
        private readonly ICacheService _cache = cache;

        public async Task<IEnumerable<UserAchievement>> CheckAndUnlockAchievementsAsync(
            Guid userId,
            AchievementTrigger trigger,
            CancellationToken cancellationToken)
        {
            var unlocked = new List<UserAchievement>();

            try
            {
                var user = await userRepository.GetByIdAsync(userId, cancellationToken);
                if (user == null)
                    return unlocked;

                var userAchievements = await achievementRepository.GetUserAchievementsAsync(userId, cancellationToken);
                var unlockedIds = userAchievements
                    .Where(a => a.IsCompleted)
                    .Select(a => a.AchievementId)
                    .ToHashSet();

                var allAchievements = await achievementRepository.GetAllAsync(cancellationToken);
                var pendingAchievements = allAchievements
                    .Where(a => !unlockedIds.Contains(a.Id))
                    .ToList();

                foreach (var achievement in pendingAchievements)
                {
                    if (await CheckCondition(user, achievement, trigger, cancellationToken))
                    {
                        var userAchievement = new UserAchievement(user, achievement);
                        userAchievement.Complete();

                        await achievementRepository.AddUserAchievementAsync(userAchievement, cancellationToken);
                        if (achievement.XpReward > 0)
                        {
                            await xpTransactionRepository.AddAsync(
                                new XpTransaction(userId, achievement.XpReward, XpSource.Achievement, achievement.Id),
                                cancellationToken);
                        }
                        unlocked.Add(userAchievement);

                        logger.LogInformation("User {UserId} unlocked achievement {AchievementId}", userId, achievement.Id);
                    }
                }

                return unlocked;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking achievements for user {UserId}", userId);
                return unlocked;
            }
        }

        private async Task<bool> CheckCondition(
            User user,
            Achievement achievement,
            AchievementTrigger trigger,
            CancellationToken cancellationToken)
        {
            return achievement.Type switch
            {
                AchievementType.Count => trigger.Value >= achievement.RequiredProgress,
                AchievementType.Streak => trigger.Value >= achievement.RequiredProgress,
                AchievementType.Milestone => user.Xp >= achievement.RequiredProgress * 100,
                AchievementType.Perfect => trigger.IsPerfect,
                AchievementType.Unique => trigger.Value == 1,
                _ => false
            };
        }

        public async Task<IEnumerable<AchievementDto>> GetAllAchievementsAsync(CancellationToken cancellationToken = default)
        {
            var all = await achievementRepository.GetAllAsync(cancellationToken);
            return all.Select(a => new AchievementDto
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                Category = a.Category,
                Rarity = a.Rarity,
                Icon = a.Icon,
                BadgeColor = a.BadgeColor,
                XpReward = a.XpReward,
                GemReward = a.GemReward,
                RequiredProgress = a.RequiredProgress,
                IsHidden = a.IsHidden
            });
        }

        public async Task<IEnumerable<UserAchievementDto>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var userAchievements = await achievementRepository.GetUserAchievementsAsync(userId, cancellationToken);

            return userAchievements.Select(ua => new UserAchievementDto
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
        }

        public async Task<IEnumerable<UserAchievementDto>> GetUnlockedAchievementsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var userAchievements = await achievementRepository.GetUserAchievementsAsync(userId, cancellationToken);
            return userAchievements
                .Where(ua => ua.IsCompleted)
                .Select(ua => new UserAchievementDto
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
        }

        public async Task<AchievementProgressDto?> GetAchievementProgressAsync(
            Guid userId,
            string achievementId,
            CancellationToken cancellationToken = default)
        {
            var all = await achievementRepository.GetAllAsync(cancellationToken);
            var achievement = all.FirstOrDefault(a => a.Id == achievementId);
            if (achievement == null)
                return null;

            var userAchievement = await achievementRepository.GetUserAchievementAsync(userId, achievementId, cancellationToken);

            return new AchievementProgressDto
            {
                AchievementId = achievement.Id,
                Name = achievement.Name,
                Description = achievement.Description,
                Icon = achievement.Icon,
                Rarity = achievement.Rarity,
                Progress = userAchievement?.Progress ?? 0,
                RequiredProgress = achievement.RequiredProgress,
                IsCompleted = userAchievement?.IsCompleted ?? false,
                CompletedAt = userAchievement?.CompletedAt
            };
        }

        public async Task<AchievementStatisticsDto> GetAchievementStatisticsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var userAchievements = await achievementRepository.GetUserAchievementsAsync(userId, cancellationToken);
            var unlocked = userAchievements.Where(ua => ua.IsCompleted).ToList();
            var all = await achievementRepository.GetAllAsync(cancellationToken);
            var total = all.Count();

            return new AchievementStatisticsDto
            {
                TotalAchievements = total,
                UnlockedCount = unlocked.Count,
                InProgressCount = userAchievements.Count(ua => !ua.IsCompleted && ua.Progress > 0),
                UnlockedPercentage = total > 0 ? (double)unlocked.Count / total * 100 : 0,
                AchievementScore = unlocked.Sum(ua => (int)ua.Achievement.Rarity + 1),
                RarityCounts = unlocked
                    .GroupBy(ua => ua.Achievement.Rarity.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentAchievements = unlocked
                    .OrderByDescending(ua => ua.CompletedAt)
                    .Take(5)
                    .Select(ua => new RecentAchievementDto
                    {
                        Id = ua.AchievementId,
                        Name = ua.Achievement.Name,
                        Icon = ua.Achievement.Icon,
                        UnlockedAt = ua.CompletedAt ?? ua.UnlockedAt
                    })
                    .ToList()
            };
        }

        public async Task<IEnumerable<AchievementLeaderboardDto>> GetAchievementLeaderboardAsync(
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            return await achievementRepository.GetAchievementLeaderboardAsync(limit, cancellationToken);
        }
    }
}
