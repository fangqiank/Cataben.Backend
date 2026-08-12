namespace Cataben.Application.DTOs
{
    public class LearningPathDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? CoverImage { get; set; }
        public string Level { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public int ChallengeCount { get; set; }
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public LearningPathStatisticsDto Statistics { get; set; } = new();

        // 每路径完成进度（按当前用户已解题实时计算；匿名用户为 0/false）。
        // 列表端点与详情端点共用同一算法，前端列表页无需 N+1 detail 调用即可渲染「已完成 X/Y 条路径」。
        public int Progress { get; set; }
        public bool IsCompleted { get; set; }
        public int CompletedChallenges { get; set; }
        public int TotalChallenges { get; set; }
    }

    public class LearningPathDetailDto : LearningPathDto
    {
        public List<ChallengeBriefDto> Challenges { get; set; } = new();
    }

    public class ChallengeBriefDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public int XpReward { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>Full-fidelity learning-path view for admin CRUD, including draft (unpublished) paths
    /// and the ordered challenge list. Statistics/progress are omitted — those are per-user.</summary>
    public class AdminLearningPathDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? CoverImage { get; set; }
        public string Level { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsPublished { get; set; }
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public List<AdminPathChallengeDto> Challenges { get; set; } = new();
    }

    public class AdminPathChallengeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderInPath { get; set; }
    }

    /// <summary>Challenge linkage input for admin create/update: which challenge, in which order.</summary>
    public class PathChallengeInput
    {
        public Guid Id { get; set; }
        public int Order { get; set; }
    }

    public class UserLearningPathProgressDto
    {
        public Guid LearningPathId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int CompletedChallenges { get; set; }
        public int TotalChallenges { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }

    public class LearningPathStatisticsDto
    {
        public int TotalUsersStarted { get; set; }
        public int TotalUsersCompleted { get; set; }
        public double AverageProgress { get; set; }
        public double CompletionRate { get; set; }
    }
}
