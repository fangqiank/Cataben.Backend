namespace Cataben.Domain.Entities
{
    public class UserLearningPath
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; } = null!;
        public Guid LearningPathId { get; private set; }
        public LearningPath LearningPath { get; private set; } = null!;
        public int Progress { get; private set; }
        public int CompletedChallenges { get; private set; }
        public int TotalChallenges { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public DateTime? LastActivityAt { get; private set; }

        private UserLearningPath() { }

        public UserLearningPath(User user, LearningPath learningPath)
        {
            Id = Guid.NewGuid();
            UserId = user.Id;
            User = user;
            LearningPathId = learningPath.Id;
            LearningPath = learningPath;
            TotalChallenges = learningPath.Challenges.Count;
            StartedAt = DateTime.UtcNow;
        }

        public void UpdateProgress(int completed)
        {
            CompletedChallenges = completed;
            Progress = TotalChallenges > 0 ? (int)((double)completed / TotalChallenges * 100) : 0;
            LastActivityAt = DateTime.UtcNow;
            if (CompletedChallenges >= TotalChallenges && !IsCompleted)
            {
                IsCompleted = true;
                CompletedAt = DateTime.UtcNow;
            }
        }
    }
}
