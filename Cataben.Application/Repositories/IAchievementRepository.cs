using Cataben.Application.DTOs;
using Cataben.Domain.Entities;

namespace Cataben.Application.Repositories
{
    public interface IAchievementRepository
    {
        Task<IEnumerable<Achievement>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>A single achievement definition by its string id.</summary>
        Task<Achievement?> GetByIdAsync(string achievementId, CancellationToken cancellationToken = default);

        /// <summary>Add a new achievement definition (catalog).</summary>
        Task AddAsync(Achievement achievement, CancellationToken cancellationToken = default);
        Task AddUserAchievementAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default);
        Task<IEnumerable<AchievementLeaderboardDto>> GetAchievementLeaderboardAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<int> GetMentorCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<HashSet<string>> GetUnlockedAchievementIdsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserAchievement?> GetUserAchievementAsync(Guid userId, string achievementId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetUserRankAsync(Guid userId, CancellationToken cancellationToken = default);
        Task UpdateUserAchievementAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default);
    }
}
