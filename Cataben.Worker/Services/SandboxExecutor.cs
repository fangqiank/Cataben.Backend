using Cataben.Domain.Enums;
using Cataben.Shared.Execution;
using Cataben.Shared.Messaging;
using System.Diagnostics;

namespace Cataben.Worker.Services;

/// <summary>
/// Compiles user code and runs the resulting assembly in a separate dotnet host process so a
/// timed-out or CPU-bound submission can be hard-killed rather than left running in the Worker.
/// </summary>
public class SandboxExecutor(
    CodeCompiler compiler,
    ProcessCodeRunner processRunner,
    ILogger<SandboxExecutor> logger)
{
    /// <summary>
    /// Legacy single-shot path: compile then run once with NO stdin. Kept because the sandbox health
    /// check (<c>SandboxHealthCheck</c>) probes the executor through it. The real submission path uses
    /// <see cref="CompileAsync"/> + <see cref="RunAsync"/> directly (see <see cref="ExecutionWorker"/>).
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(ExecutionMessage message, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        try
        {
            logger.LogInformation("Executing code for {ExecutionId}", message.ExecutionId);

            var compilation = await CompileAsync(message.Code, cancellationToken);
            if (!compilation.Success)
            {
                return new ExecutionResult
                {
                    IsSuccessful = false,
                    Error = string.Join("\n", compilation.Errors ?? new[] { "Compilation failed" }),
                    Status = SubmissionStatus.Failed
                };
            }

            var run = await RunAsync(compilation.AssemblyBytes!, stdin: null, message.TimeoutSeconds, cancellationToken);

            stopwatch.Stop();
            logger.LogInformation("Execution {ExecutionId} completed in {Elapsed}ms",
                message.ExecutionId, stopwatch.ElapsedMilliseconds);

            return new ExecutionResult
            {
                IsSuccessful = run.Success,
                Output = run.Output,
                // Surface the failure reason when the run failed; otherwise stderr.
                Error = run.Success ? run.Error : run.FailureDetail,
                Status = run.Status,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                MemoryAllocatedBytes = GC.GetTotalMemory(forceFullCollection: true) - memoryBefore
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("Execution {ExecutionId} was cancelled", message.ExecutionId);
            return new ExecutionResult
            {
                IsSuccessful = false,
                Error = "Execution was cancelled",
                Status = SubmissionStatus.Cancelled,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Execution {ExecutionId} failed", message.ExecutionId);
            return new ExecutionResult
            {
                IsSuccessful = false,
                Error = ex.Message,
                Status = SubmissionStatus.SystemError,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>Forwards to <see cref="CodeCompiler.CompileAsync"/>.</summary>
    public Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken)
        => compiler.CompileAsync(code, cancellationToken);

    public async Task<SingleRunResult> RunAsync(
        byte[] assemblyBytes,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var run = await processRunner.RunAsync(assemblyBytes, stdin, timeoutSeconds, cancellationToken);
        return new SingleRunResult(
            Success: run.Success,
            Output: run.Output,
            Error: run.Error,
            FailureDetail: run.FailureDetail,
            Status: run.Status);
    }
}

/// <summary>
/// Outcome of a single run of user code (one stdin feed). <see cref="Success"/> means the entry point
/// returned within the timeout without throwing; <see cref="Status"/> classifies any failure.
/// <list type="bullet">
/// <item><see cref="Output"/> / <see cref="Error"/>: captured stdout / stderr.</item>
/// <item><see cref="FailureDetail"/>: the timeout or unwrapped-exception reason (null on success).</item>
/// </list>
/// </summary>
public sealed record SingleRunResult(
    bool Success,
    string Output,
    string Error,
    string? FailureDetail,
    SubmissionStatus Status);
