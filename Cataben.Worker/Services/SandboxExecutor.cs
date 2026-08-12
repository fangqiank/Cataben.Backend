using Cataben.Application.DTOs;
using Cataben.Domain.Enums;
using System.Diagnostics;

namespace Cataben.Worker.Services
{
    public class SandboxExecutor(ILogger<SandboxExecutor> logger)
    {
        public async Task<ExecutionResultDto> ExecuteAsync(
        ExecutionMessage message,
        CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 实际实现: 在沙箱中执行代码
                // 这里使用模拟实现

                await Task.Delay(100, cancellationToken);

                stopwatch.Stop();

                return new ExecutionResultDto
                {
                    IsSuccessful = true,
                    Output = "Code executed successfully\nResult: 42",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    MemoryAllocatedBytes = 1024,
                    Score = 100,
                    TotalScore = 100,
                    Status = SubmissionStatus.Completed
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing code");
                return new ExecutionResultDto
                {
                    IsSuccessful = false,
                    Error = ex.Message,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Status = SubmissionStatus.Failed
                };
            }
        }
    }

    public class ExecutionMessage
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? ChallengeId { get; set; }
        public Guid UserId { get; set; }
        public ChallengeType Type { get; set; }
        public bool IsSubmission { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 10;
        public int MemoryLimitMb { get; set; } = 256;
        public List<TestCaseDto> TestCases { get; set; } = new();
        public List<HiddenTestDto> HiddenTests { get; set; } = new();
        public string? DatabaseSchema { get; set; }
        public string? SeedDataScript { get; set; }
        public string? OptimalQuery { get; set; }
    }

    public class HiddenTestDto : TestCaseDto
    {
        public string ValidationType { get; set; } = "exact";
        public int MinScore { get; set; }
        public TimeSpan? MaxExecutionTime { get; set; }
        public long? MaxMemoryUsage { get; set; }
    }
}
