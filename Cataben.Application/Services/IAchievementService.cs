using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;

namespace Cataben.Application.Services
{
    public interface IAchievementService
    {
        Task<IEnumerable<AchievementDto>> GetAllAchievementsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<UserAchievementDto>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserAchievementDto>> GetUnlockedAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<AchievementProgressDto?> GetAchievementProgressAsync(Guid userId, string achievementId, CancellationToken cancellationToken = default);
        Task<AchievementStatisticsDto> GetAchievementStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<AchievementLeaderboardDto>> GetAchievementLeaderboardAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserAchievement>> CheckAndUnlockAchievementsAsync(Guid userId, AchievementTrigger trigger, CancellationToken cancellationToken = default);
    }

    public class AchievementTrigger
    {
        public AchievementType Type { get; set; }
        public int Value { get; set; }
        public Guid? ChallengeId { get; set; }
        public bool IsPerfect { get; set; }
        public int Score { get; set; }
        public int ExecutionTimeMs { get; set; }
    }
}
