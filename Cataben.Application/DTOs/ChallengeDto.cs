using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class ChallengeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ChallengeType Type { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string InitialCode { get; set; } = string.Empty;
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public int TimeLimitSeconds { get; set; }
        public int MemoryLimitMb { get; set; }
        public int SuccessRate { get; set; }
        public int TotalSubmissions { get; set; }
        public bool IsSolved { get; set; }
        public List<string> Hints { get; set; } = new();
        public List<TestCaseDto> TestCases { get; set; } = new();
    }
    public class TestCaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public int Weight { get; set; }
    }

    /// <summary>Full-fidelity challenge view for admin CRUD. Unlike the public ChallengeDto, this
    /// exposes SolutionCode, IsActive, all test cases (incl. private), and path linkage.</summary>
    public class AdminChallengeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ChallengeType Type { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string InitialCode { get; set; } = string.Empty;
        public string SolutionCode { get; set; } = string.Empty;
        public List<string> Hints { get; set; } = new();
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public int TimeLimitSeconds { get; set; }
        public int MemoryLimitMb { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? LearningPathId { get; set; }
        public int OrderInPath { get; set; }
        public List<TestCaseDto> TestCases { get; set; } = new();
    }

    /// <summary>Today's featured challenge (GET /api/challenge/daily).</summary>
    public class DailyChallengeDto
    {
        public DateTime Date { get; set; }
        public ChallengeDto Challenge { get; set; } = new();
    }

    /// <summary>POST /api/challenge/{id}/reveal — consumes one global reveal credit, returns the reference solution.</summary>
    public class RevealSolutionResultDto
    {
        public string SolutionCode { get; set; } = string.Empty;
        public int RevealsRemaining { get; set; }
    }
}
