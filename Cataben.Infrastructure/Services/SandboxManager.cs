using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Cataben.Infrastructure.Services
{
    public class SandboxManager(ILogger<SandboxManager> logger) : ISandboxManager
    {
        private readonly SemaphoreSlim _semaphore = new(10); // Max concurrent sandboxes
        private readonly ConcurrentDictionary<string, SandboxInstance> _activeSandboxes = new();

        public async Task<SandboxExecutionResult> ExecuteInSandboxAsync(
            Assembly assembly,
            Dictionary<string, object> parameters,
            ExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var sandboxId = Guid.NewGuid().ToString();
            SandboxInstance? sandbox = null;

            try
            {
                await _semaphore.WaitAsync(cancellationToken);

                // Track the active sandbox.
                sandbox = new SandboxInstance
                {
                    Id = sandboxId,
                    CreatedAt = DateTime.UtcNow
                };
                _activeSandboxes[sandboxId] = sandbox;

                // Enforce the configured timeout. NOTE: .NET has no AppDomains and cannot
                // forcibly abort in-process CPU-bound code. We cancel the token (well-behaved
                // code can observe it) AND race the execution against a delay so the caller
                // is not blocked indefinitely. The rogue task, however, may keep running until
                // it finishes on its own — a real process/container sandbox is required for a
                // hard kill. (TODO: out-of-process isolation.)
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(options.Timeout);

                var executionTask = Task.Run(() => ExecuteInIsolation(assembly, parameters, options), cts.Token);
                var winner = await Task.WhenAny(executionTask, Task.Delay(options.Timeout, cancellationToken));

                if (winner != executionTask)
                {
                    logger.LogWarning("Sandbox execution {SandboxId} exceeded timeout {TimeoutMs}ms", sandboxId, (long)options.Timeout.TotalMilliseconds);
                    return new SandboxExecutionResult
                    {
                        Success = false,
                        Error = $"Execution timed out after {options.Timeout.TotalSeconds:F0}s"
                    };
                }

                var result = await executionTask;

                return new SandboxExecutionResult
                {
                    Success = result.Success,
                    Output = result.Output,
                    Error = result.Error,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    MemoryAllocatedBytes = result.MemoryAllocatedBytes,
                    QueryPlan = result.QueryPlan
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

        private IsolationResult ExecuteInIsolation(
        Assembly assembly,
        Dictionary<string, object> parameters,
        ExecutionOptions options)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            var originalOut = Console.Out;
            var originalError = Console.Error;

            try
            {
                using var stringWriter = new StringWriter(output);
                using var errorWriter = new StringWriter(error);
                Console.SetOut(stringWriter);
                Console.SetError(errorWriter);

                var stopwatch = Stopwatch.StartNew();
                var memoryBefore = GC.GetTotalMemory(true);

                var entryPoint = assembly.EntryPoint;
                if (entryPoint == null)
                {
                    return new IsolationResult
                    {
                        Success = false,
                        Error = "No entry point found"
                    };
                }

                // Convert parameters to string array
                var args = new[] { JsonSerializer.Serialize(parameters) };
                var result = entryPoint.Invoke(null, new object[] { args });

                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(true);

                return new IsolationResult
                {
                    Success = true,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    MemoryAllocatedBytes = memoryAfter - memoryBefore
                };
            }
            catch (Exception ex)
            {
                return new IsolationResult
                {
                    Success = false,
                    Error = ex.Message,
                    Output = output.ToString()
                };
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private class SandboxInstance
        {
            public string Id { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsActive { get; set; } = true;
        }

        private class IsolationResult
        {
            public bool Success { get; set; }
            public string? Output { get; set; }
            public string? Error { get; set; }
            public long ExecutionTimeMs { get; set; }
            public long MemoryAllocatedBytes { get; set; }
            public string? QueryPlan { get; set; }
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
