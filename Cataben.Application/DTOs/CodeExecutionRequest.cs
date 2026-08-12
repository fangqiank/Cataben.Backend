using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class CodeExecutionRequest
    {
        public string Code { get; set; } = string.Empty;
        public Guid? ChallengeId { get; set; }
        public ChallengeType Type { get; set; } = ChallengeType.Algorithm;
        public Dictionary<string, object>? Parameters { get; set; }
        public bool IsSubmission { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }

    public class ExecutionResultDto
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
        public List<TestResultDto> TestResults { get; set; } = new();
        public SubmissionStatus Status { get; set; }
    }

    public class TestResultDto
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public int Score { get; set; }
        public string? Message { get; set; }
        public long ExecutionTimeMs { get; set; }
    }
}
