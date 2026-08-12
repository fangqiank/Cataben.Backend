namespace Cataben.Application.DTOs
{
    public class LeaderboardDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; }
        public int Score { get; set; }
        public int Xp { get; set; }
        public int Level { get; set; }
        public int SolvedChallenges { get; set; }
        public int Achievements { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }
    public class AchievementLeaderboardDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Score { get; set; }
        public int AchievementsCount { get; set; }
        public int Rank { get; set; }
    }
}
