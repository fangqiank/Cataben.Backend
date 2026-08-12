namespace Cataben.Domain.Entities
{
    public class UserAchievement
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; } = null!;
        public string AchievementId { get; private set; } = string.Empty;
        public Achievement Achievement { get; private set; } = null!;
        public DateTime UnlockedAt { get; private set; }
        public int Progress { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; } = new();

        private UserAchievement() { }

        public UserAchievement(User user, Achievement achievement)
        {
            Id = Guid.NewGuid();
            UserId = user.Id;
            User = user;
            AchievementId = achievement.Id;
            Achievement = achievement;
            UnlockedAt = DateTime.UtcNow;
            Progress = 0;
            IsCompleted = false;
        }

        public void UpdateProgress(int progress)
        {
            Progress = Math.Min(progress, Achievement.RequiredProgress);
            if (Progress >= Achievement.RequiredProgress && !IsCompleted)
            {
                Complete();
            }
        }

        public void IncrementProgress(int amount = 1)
        {
            UpdateProgress(Progress + amount);
        }

        public void Complete()
        {
            // Idempotent: a completed achievement must not award rewards twice.
            if (IsCompleted)
                return;

            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
            if (Achievement.XpReward > 0)
                User.AddXp(Achievement.XpReward);

            if (Achievement.GemReward > 0)
                User.AddGems(Achievement.GemReward);
        }
    }
}
