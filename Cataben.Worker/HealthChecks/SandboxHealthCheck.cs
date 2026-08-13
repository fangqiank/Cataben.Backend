using Cataben.Shared.Messaging;
using Cataben.Worker.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cataben.Worker.HealthChecks
{
    public class SandboxHealthCheck : IHealthCheck
    {
        private readonly CodeCompiler _compiler;
        private readonly SandboxExecutor _executor;
        private readonly ILogger<SandboxHealthCheck> _logger;

        public SandboxHealthCheck(
            CodeCompiler compiler,
            SandboxExecutor executor,
            ILogger<SandboxHealthCheck> logger)
        {
            _compiler = compiler;
            _executor = executor;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Run a simple test code
                var testCode = @"
                    class Program {
                        static void Main() {
                            Console.WriteLine(""HEALTH_CHECK_OK"");
                        }
                    }";

                var message = new ExecutionMessage
                {
                    ExecutionId = "health-check",
                    Code = testCode,
                    TimeoutSeconds = 5,
                    MemoryLimitMb = 50,
                    HasTests = false,
                    Parameters = new Dictionary<string, object>()
                };

                var result = await _executor.ExecuteAsync(message, cancellationToken);

                if (result.IsSuccessful && result.Output?.Contains("HEALTH_CHECK_OK") == true)
                {
                    return HealthCheckResult.Healthy("Sandbox is operational");
                }
                else
                {
                    return HealthCheckResult.Degraded(
                        "Sandbox is operational but returned unexpected output",
                        new Exception(result.Error));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sandbox health check failed");
                return HealthCheckResult.Unhealthy("Sandbox is not responding", ex);
            }
        }
    }
}
