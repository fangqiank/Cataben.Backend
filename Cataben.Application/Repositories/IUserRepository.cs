using Cataben.Domain.Entities;

namespace Cataben.Application.Repositories
{
    public interface IUserRepository
    {
        Task AddAsync(User user, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByIdBasicAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetTopUsersByXpAsync(int count, CancellationToken cancellationToken = default);
        Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
        Task<UserAchievementStats> GetUserAchievementStatsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserAchievement>> GetUserCompletedAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetUserRankAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetCompletedAchievementCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    }

    public class UserAchievementStats
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int TotalXpEarned { get; set; }
        public int TotalGemsEarned { get; set; }
        public Dictionary<string, int> ByRarity { get; set; } = new();
    }

    /// <summary>时段排行榜的一行：UserId/Username/AvatarUrl/LastActiveAt 来自 User；
    /// TotalXp=累计 XP（用于算等级）；PeriodXp=窗口内解题 XP 之和；PeriodSolved=窗口内成功解题数。</summary>
    public class LeaderboardPeriodEntry
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int TotalXp { get; set; }
        public int PeriodXp { get; set; }
        public int PeriodSolved { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }
}
