using MediatR;

namespace Cataben.Application.Queries
{
    public class GetUserProgressQuery: IRequest<UserProgressDto>
    {
        public Guid UserId { get; set; }
    }

    public class UserProgressDto
    {
        public int TotalChallenges { get; set; }
        public int CompletedChallenges { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public int Level { get; set; }
        public int XpToNextLevel { get; set; }
        public Dictionary<string, int> CategoryProgress { get; set; } = new();
        public List<LearningPathProgressDto> LearningPaths { get; set; } = new();
    }

    public class LearningPathProgressDto
    {
        public Guid LearningPathId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Progress { get; set; }
        public bool IsCompleted { get; set; }
    }
}
