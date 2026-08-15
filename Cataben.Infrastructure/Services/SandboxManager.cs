using System.Collections.Concurrent;
using Cataben.Shared.Execution;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Services
{
    public class SandboxManager(
        ProcessCodeRunner processRunner,
        ILogger<SandboxManager> logger) : ISandboxManager
    {
        private readonly SemaphoreSlim _semaphore = new(10);
        private readonly ConcurrentDictionary<string, SandboxInstance> _activeSandboxes = new();

        public async Task<SandboxExecutionResult> ExecuteInSandboxAsync(
            byte[] assemblyBytes,
            Dictionary<string, object> parameters,
            ExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var sandboxId = Guid.NewGuid().ToString();
            SandboxInstance? sandbox = null;

            try
            {
                await _semaphore.WaitAsync(cancellationToken);

                sandbox = new SandboxInstance
                {
                    Id = sandboxId,
                    CreatedAt = DateTime.UtcNow
                };
                _activeSandboxes[sandboxId] = sandbox;

                var timeoutSeconds = Math.Max(1, (int)options.Timeout.TotalSeconds);
                var result = await processRunner.RunAsync(
                    assemblyBytes,
                    stdin: ExtractStdin(parameters),
                    timeoutSeconds,
                    cancellationToken);

                return new SandboxExecutionResult
                {
                    Success = result.Success,
                    Output = result.Output,
                    Error = result.Error,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    // Peak child working set — the closest honest memory metric the
                    // out-of-process runner can observe. QueryPlan stays null: the in-process
                    // runner has no SQL engine, so it never fabricates a plan.
                    MemoryAllocatedBytes = result.PeakMemoryBytes
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sandbox execution failed for {SandboxId}", sandboxId);
                return new SandboxExecutionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
            finally
            {
                if (sandbox != null)
                {
                    _activeSandboxes.TryRemove(sandboxId, out _);
                }
                _semaphore.Release();
            }
        }

        /// <summary>Pulls the run-code stdin from the request's Parameters dictionary.
        /// Model binding deserializes JSON values to JsonElement, so both a plain string
        /// and a boxed JsonElement(string) are accepted; anything else is ignored.</summary>
        private static string? ExtractStdin(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("stdin", out var value))
                return null;

            return value switch
            {
                string s => s,
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je
                    => je.GetString(),
                _ => null
            };
        }

        private class SandboxInstance
        {
            public string Id { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsActive { get; set; } = true;
        }
    }

    public class SandboxExecutionResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long MemoryAllocatedBytes { get; set; }
        public string? QueryPlan { get; set; }
    }
}
