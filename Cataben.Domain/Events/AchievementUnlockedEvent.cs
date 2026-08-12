using Cataben.Domain.Entities;

namespace Cataben.Domain.Events
{
    public class AchievementUnlockedEvent(UserAchievement userAchievement)
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; } = userAchievement.UserId;
        public string AchievementId { get; private set; } = userAchievement.AchievementId;
        public string AchievementName { get; private set; } = userAchievement.Achievement.Name;
        public string AchievementIcon { get; private set; } = userAchievement.Achievement.Icon;
        public int XpReward { get; private set; } = userAchievement.Achievement.XpReward;
        public int GemReward { get; private set; } = userAchievement.Achievement.GemReward;
        public DateTime UnlockedAt { get; private set; } = userAchievement.CompletedAt ?? userAchievement.UnlockedAt;
    }
}
