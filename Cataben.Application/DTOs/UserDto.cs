using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public int Xp { get; set; }
        public int Gems { get; set; }
        public int Level { get; set; }
        public int SubmissionsCount { get; set; }
        public int SuccessfulSubmissions { get; set; }
        public int AchievementsCount { get; set; }
        public int CurrentStreak { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public int RevealsRemaining { get; set; }
    }
    public class UserStatisticsDto
    {
        public int TotalXp { get; set; }
        public int Level { get; set; }
        public int Gems { get; set; }
        public int Submissions { get; set; }
        public int SolvedChallenges { get; set; }
        public int TotalChallenges { get; set; }
        public double SuccessRate { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public int Achievements { get; set; }
        public int LearningPathsCompleted { get; set; }
        public Dictionary<string, int> CategoryStats { get; set; } = new();
    }

    /// <summary>Per-day submission counts for the activity heatmap (GET /api/user/me/activity).</summary>
    public class ActivityDto
    {
        public List<ActivityDayDto> Days { get; set; } = new();
    }

    public class ActivityDayDto
    {
        public DateTime Date { get; set; }   // UTC midnight
        public int Count { get; set; }
    }
}
