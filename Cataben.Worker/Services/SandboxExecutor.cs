using Cataben.Domain.Enums;
using Cataben.Shared.Messaging;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Cataben.Worker.Services;

/// <summary>
/// Compiles user code and runs the resulting assembly in-process. No true isolation (no AppDomains):
/// a timed-out / CPU-bound task cannot be forcibly killed and may run to completion in the background
/// (TODO: out-of-process/container isolation). Console In/Out/Error are redirected per run and restored
/// in <c>finally</c>.
/// </summary>
public class SandboxExecutor(CodeCompiler compiler, ILogger<SandboxExecutor> logger)
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

            var run = await RunAsync(compilation.Assembly!, stdin: null, message.TimeoutSeconds, cancellationToken);

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

    /// <summary>
    /// Run an already-compiled assembly once, optionally feeding <paramref name="stdin"/>. Redirects
    /// Console In/Out/Error for the run and restores them in <c>finally</c>.
    /// </summary>
    /// <remarks>
    /// <b>Known limitation:</b> invoking the same entry point multiple times against one Assembly is
    /// subject to cross-run static-state pollution (no AppDomain unload). Runs are independent ONLY for
    /// stateless user code — challenge authors writing Algorithm tasks should avoid <c>static</c> state
    /// in <c>Main</c>. A null <paramref name="stdin"/> wires <see cref="Console.In"/> to
    /// <see cref="StreamReader.Null"/> so user code that reads input hits EOF rather than the host's stdin.
    /// </remarks>
    public async Task<SingleRunResult> RunAsync(
        Assembly assembly,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalIn = Console.In;

        try
        {
            using var stringWriter = new StringWriter(output);
            using var errorWriter = new StringWriter(error);
            // null/empty stdin → empty StringReader: user ReadLine() returns null (EOF) immediately,
            // so user code that reads input can't block on the host's console stdin.
            using var inputReader = new StringReader(stdin ?? string.Empty);
            Console.SetOut(stringWriter);
            Console.SetError(errorWriter);
            Console.SetIn(inputReader);

            var entryPoint = assembly.EntryPoint
                ?? throw new InvalidOperationException("Compiled assembly has no entry point.");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // Match the entry point signature: parameterless Main() takes no args; Main(string[]) takes
            // one string[]. (Historic bug: passing string[] to a parameterless Main threw
            // TargetParameterCountException and was misreported as a spurious SystemError.)
            var paramCount = entryPoint.GetParameters().Length;
            object?[]? invokeArgs = paramCount == 0 ? null : new object?[] { Array.Empty<string>() };

            await Task.Run(() => entryPoint.Invoke(null, invokeArgs), cts.Token);

            return new SingleRunResult(
                Success: true,
                Output: output.ToString(),
                Error: error.ToString(),
                FailureDetail: null,
                Status: SubmissionStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            // Distinguish parent cancellation (propagate intent) from our own timeout (report Timeout).
            if (cancellationToken.IsCancellationRequested)
                throw;

            return new SingleRunResult(
                Success: false,
                Output: output.ToString(),
                Error: error.ToString(),
                FailureDetail: $"Run exceeded the {timeoutSeconds}s time limit",
                Status: SubmissionStatus.Timeout);
        }
        catch (Exception ex)
        {
            return new SingleRunResult(
                Success: false,
                Output: output.ToString(),
                Error: error.ToString(),
                FailureDetail: Unwrap(ex),
                Status: SubmissionStatus.SystemError);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Console.SetIn(originalIn);
        }
    }

    /// <summary>Drill through reflection wrappers to the real user-code exception message.</summary>
    private static string Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: not null } tie)
            ex = tie.InnerException!;
        return ex.Message;
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
