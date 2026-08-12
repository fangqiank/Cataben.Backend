using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class AchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AchievementCategory Category { get; set; }
        public AchievementRarity Rarity { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = string.Empty;
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsHidden { get; set; }
    }
    public class UserAchievementDto
    {
        public Guid Id { get; set; }
        public string AchievementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AchievementCategory Category { get; set; }
        public AchievementRarity Rarity { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime UnlockedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
    public class AchievementProgressDto
    {
        public string AchievementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public AchievementRarity Rarity { get; set; }
        public int Progress { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
    public class AchievementStatisticsDto
    {
        public int TotalAchievements { get; set; }
        public int UnlockedCount { get; set; }
        public int InProgressCount { get; set; }
        public double UnlockedPercentage { get; set; }
        public int AchievementScore { get; set; }
        public Dictionary<string, int> RarityCounts { get; set; } = new();
        public List<RecentAchievementDto> RecentAchievements { get; set; } = new();
    }
    public class RecentAchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime UnlockedAt { get; set; }
    }
}
