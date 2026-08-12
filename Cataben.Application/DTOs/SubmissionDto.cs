using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class SubmissionDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ChallengeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; }
        public bool IsSuccessful { get; set; }
        public int Score { get; set; }
        public int TotalScore { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long MemoryUsedBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public string? QueryPlan { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<TestResultDto> TestResults { get; set; } = new();
        public List<StatusHistoryDto> StatusHistory { get; set; } = new();
        public double ScorePercentage { get; set; }
    }
    public class StatusHistoryDto
    {
        public SubmissionStatus Status { get; set; }
        public string? Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }
    public class SubmissionResultDto
    {
        public Guid SubmissionId { get; set; }
        public SubmissionStatus Status { get; set; }
        public bool IsSuccessful { get; set; }
        public int Score { get; set; }
        public int TotalScore { get; set; }
        public double ScorePercentage { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long MemoryUsedKB { get; set; }
        public List<TestResultDto> TestResults { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
