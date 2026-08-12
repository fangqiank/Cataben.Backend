using Cataben.Worker.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Services
{
    public class ExecutionWorker
        (
            IMessageBus messageBus,
            SandboxExecutor executor,
            TestRunner testRunner,
            ILogger<ExecutionWorker> logger,
            IServiceProvider serviceProvider
        ) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ExecutionWorker started");

            await messageBus.SubscribeAsync<ExecutionMessage>("code.execute", async (message) =>
            {
                await ProcessExecutionMessage(message, stoppingToken);
            });

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessExecutionMessage(ExecutionMessage message, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Processing execution {ExecutionId}", message.ExecutionId);

                // 1. Execute code in sandbox
                var executionResult = await executor.ExecuteAsync(message, cancellationToken);

                // 2. Run tests if it's a submission
                if (message.IsSubmission && message.ChallengeId.HasValue)
                {
                    var testResults = await testRunner.RunTestsAsync(
                        executionResult.Output ?? "",
                        message.TestCases,
                        message.HiddenTests,
                        cancellationToken);

                    // Calculate score: totalScore = sum of ALL test weights (max possible);
                    // score = sum of weights for PASSED tests (each TestResultDto.Score is weight-if-passed else 0).
                    var totalScore = message.TestCases.Sum(t => t.Weight) + message.HiddenTests.Sum(t => t.Weight);
                    var score = testResults.Sum(t => t.Score);

                    executionResult.Score = score;
                    executionResult.TotalScore = totalScore;
                    executionResult.TestResults = testResults;
                    executionResult.IsSuccessful = totalScore > 0 && score >= totalScore * 0.8;
                }

                // 3. Publish result
                var resultMessage = new ExecutionResultMessage
                {
                    Success = executionResult.IsSuccessful,
                    Output = executionResult.Output,
                    Error = executionResult.Error,
                    ExecutionTimeMs = executionResult.ExecutionTimeMs,
                    MemoryAllocatedBytes = executionResult.MemoryAllocatedBytes,
                    QueryPlan = executionResult.QueryPlan,
                    Score = executionResult.Score,
                    TotalScore = executionResult.TotalScore,
                    TestResults = executionResult.TestResults.Select(t => new TestResultMessage
                    {
                        Name = t.Name,
                        Passed = t.Passed,
                        Score = t.Score,
                        Message = t.Message,
                        ExecutionTimeMs = t.ExecutionTimeMs
                    }).ToList()
                };

                await messageBus.PublishAsync($"code.result.{message.ExecutionId}", resultMessage);

                logger.LogInformation("Execution {ExecutionId} completed successfully", message.ExecutionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing execution {ExecutionId}", message.ExecutionId);

                // Send error result
                var errorResult = new ExecutionResultMessage
                {
                    Success = false,
                    Error = ex.Message,
                    Score = 0,
                    TotalScore = 0,
                    TestResults = new List<TestResultMessage>()
                };

                await messageBus.PublishAsync($"code.result.{message.ExecutionId}", errorResult);
            }
        }
    }

    public class ExecutionResultMessage
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long MemoryAllocatedBytes { get; set; }
        public string? QueryPlan { get; set; }
        public int Score { get; set; }
        public int TotalScore { get; set; }
        public List<TestResultMessage> TestResults { get; set; } = new();
    }

    public class TestResultMessage
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public int Score { get; set; }
        public string? Message { get; set; }
        public long ExecutionTimeMs { get; set; }
    }
}
