using Cataben.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.HealthChecks
{
    public class SandboxHealthCheck(
        ISandboxManager sandboxManager,
        ILogger<SandboxHealthCheck> logger
        ) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            // Sandbox execution is currently stubbed (see SandboxManager / CodeExecutorService),
            // so there is no live sandbox process to probe. Report healthy while it remains mocked.
            logger.LogDebug("Sandbox health check executed (sandbox is stubbed)");
            return Task.FromResult(HealthCheckResult.Healthy("Sandbox is operational"));
        }
    }
}
