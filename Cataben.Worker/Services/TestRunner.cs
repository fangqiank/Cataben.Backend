using Cataben.Application.DTOs;

namespace Cataben.Worker.Services
{
    public class TestRunner(ILogger<TestRunner> logger)
    {
        public async Task<List<TestResultDto>> RunTestsAsync(
            string output,
            List<TestCaseDto> testCases,
            List<HiddenTestDto> hiddenTests,
            CancellationToken cancellationToken)
        {
            var results = new List<TestResultDto>();

            foreach (var test in testCases)
            {
                var passed = await RunSingleTestAsync(output, test, cancellationToken);
                results.Add(new TestResultDto
                {
                    Name = test.Name,
                    Passed = passed,
                    Score = passed ? test.Weight : 0,
                    Message = passed ? "Passed" : "Failed",
                    ExecutionTimeMs = 10
                });
            }

            foreach (var test in hiddenTests)
            {
                var passed = await RunSingleTestAsync(output, test, cancellationToken);
                results.Add(new TestResultDto
                {
                    Name = test.Name,
                    Passed = passed,
                    Score = passed ? test.Weight : 0,
                    Message = passed ? "Passed" : "Failed",
                    ExecutionTimeMs = 10
                });
            }

            return results;
        }

        private async Task<bool> RunSingleTestAsync(
            string output, 
            TestCaseDto test, 
            CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);

            // 规范化后精确比较（避免 "1" 误匹配 "100" 这类子串问题）
            if (string.IsNullOrEmpty(test.ExpectedOutput))
                return true;

            return output.Trim().Equals(test.ExpectedOutput.Trim(), StringComparison.Ordinal);
        }
    }
}
