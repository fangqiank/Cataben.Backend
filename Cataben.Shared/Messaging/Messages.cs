using Cataben.Domain.Enums;

namespace Cataben.Shared.Messaging;

/// <summary>
/// Wire contracts shared between Cataben.API (publisher/consumer) and Cataben.Worker
/// (consumer/publisher) over NATS. Lives in Shared because the two hosts cannot share
/// a project reference directly (Infrastructure → Worker already, never the inverse).
/// </summary>

public class ExecutionMessage
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? ChallengeId { get; set; }
    public Guid UserId { get; set; }
    public Guid SubmissionId { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int MemoryLimitMb { get; set; } = 256;
    public bool HasTests { get; set; }
    // Needed so the Worker can decide whether to feed per-case stdin (only
    // Algorithm challenges honor the per-case Input field).
    public ChallengeType ChallengeType { get; set; } = ChallengeType.Algorithm;
    public List<TestCaseDto> TestCases { get; set; } = new();
    public List<HiddenTestDto> HiddenTests { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? DatabaseSchema { get; set; }
    public string? SeedDataScript { get; set; }
    public string? OptimalQuery { get; set; }
}

public class TestCaseDto
{
    public string Name { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public int Weight { get; set; } = 1;
    public string ValidationType { get; set; } = "exact";
}

public class HiddenTestDto
{
    public string Name { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public string ValidationType { get; set; } = "exact";
    public int Weight { get; set; } = 1;
    public int MinScore { get; set; }
    public TimeSpan? MaxExecutionTime { get; set; }
    public long? MaxMemoryUsage { get; set; }
}

public class ExecutionResult
{
    public bool IsSuccessful { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public string? QueryPlan { get; set; }
    public int? QueryCost { get; set; }
    public int Score { get; set; }
    public int TotalScore { get; set; }
    public SubmissionStatus Status { get; set; }
    public List<TestResult> TestResults { get; set; } = new();
}

public class TestResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public int Score { get; set; }
    public string? Message { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MemoryUsedBytes { get; set; }
}

public class ExecutionResultMessage
{
    public string ExecutionId { get; set; } = string.Empty;

    // Correlation back to the originating submission/user/challenge so the API-side
    // result consumer can persist + run gamification without an extra lookup mapping.
    public Guid SubmissionId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ChallengeId { get; set; }

    public bool IsSuccessful { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public string? QueryPlan { get; set; }
    public int? QueryCost { get; set; }
    public int Score { get; set; }
    public int TotalScore { get; set; }
    public SubmissionStatus Status { get; set; }
    public List<TestResultMessage> TestResults { get; set; } = new();
}

public class TestResultMessage
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public int Score { get; set; }
    public string? Message { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MemoryUsedBytes { get; set; }
}

public class SubmissionStatusMessage
{
    public Guid SubmissionId { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HealthCheckMessage
{
    public string CorrelationId { get; set; } = string.Empty;
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public int ActiveTasks { get; set; }
    public int MaxTasks { get; set; }
    public long MemoryUsage { get; set; }
    public TimeSpan Uptime { get; set; }
}
