using Cataben.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace Cataben.Shared.Execution;

public sealed record ProcessRunResult(
    bool Success,
    string Output,
    string Error,
    string? FailureDetail,
    SubmissionStatus Status,
    long ExecutionTimeMs,
    long PeakMemoryBytes = 0);

/// <summary>
/// Runs a compiled user assembly in a separate dotnet host process so the caller can hard-kill
/// runaway code on timeout instead of relying on cooperative cancellation inside its own process.
/// </summary>
public sealed class ProcessCodeRunner(ILogger<ProcessCodeRunner> logger)
{
    public async Task<ProcessRunResult> RunAsync(
        byte[] assemblyBytes,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (assemblyBytes.Length == 0)
            throw new InvalidOperationException("Cannot run an empty assembly.");

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"cataben-code-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        var assemblyPath = Path.Combine(workingDirectory, "user-code.dll");
        var runtimeConfigPath = Path.Combine(workingDirectory, "user-code.runtimeconfig.json");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await File.WriteAllBytesAsync(assemblyPath, assemblyBytes, cancellationToken);
            await File.WriteAllTextAsync(
                runtimeConfigPath,
                RuntimeConfigJson,
                Encoding.UTF8,
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveDotNetHost(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(runtimeConfigPath);
            startInfo.ArgumentList.Add(assemblyPath);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Failed to start code execution process.");

            // The CTS must cover the stdin write too, not just the wait below: a child that
            // never reads stdin (with input larger than the OS pipe buffer) blocks the write
            // forever — without the token there is no timeout at all, the parent hangs and
            // leaks its concurrency slot.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                if (string.IsNullOrEmpty(stdin))
                {
                    process.StandardInput.Close();
                }
                else
                {
                    await process.StandardInput.WriteAsync(stdin.AsMemory(), cts.Token);
                    process.StandardInput.Close();
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                KillProcessTree(process);
                return new ProcessRunResult(
                    false,
                    string.Empty,
                    string.Empty,
                    $"Run exceeded the {timeoutSeconds}s time limit while writing stdin",
                    SubmissionStatus.Timeout,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                throw;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                KillProcessTree(process);
                await Task.WhenAll(outputTask, errorTask);
                return new ProcessRunResult(
                    false,
                    await outputTask,
                    await errorTask,
                    $"Run exceeded the {timeoutSeconds}s time limit",
                    SubmissionStatus.Timeout,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                throw;
            }

            stopwatch.Stop();
            var output = await outputTask;
            var error = await errorTask;
            var peakMemory = GetPeakMemoryBytes(process);

            if (process.ExitCode != 0)
            {
                // A non-zero exit is the USER program failing (unhandled exception,
                // Environment.Exit(n)) — a submission failure, not a platform fault.
                // SystemError is reserved for infrastructure failures and would skip
                // test evaluation entirely.
                var detail = string.IsNullOrWhiteSpace(error)
                    ? $"Process exited with code {process.ExitCode}"
                    : error.Trim();
                return new ProcessRunResult(
                    false,
                    output,
                    error,
                    detail,
                    SubmissionStatus.Failed,
                    stopwatch.ElapsedMilliseconds,
                    peakMemory);
            }

            return new ProcessRunResult(
                true,
                output,
                error,
                null,
                SubmissionStatus.Completed,
                stopwatch.ElapsedMilliseconds,
                peakMemory);
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    /// <summary>Best-effort peak memory of the child process (working set, bytes) —
    /// the closest externally-observable proxy for "memory used" the runner has.</summary>
    private static long GetPeakMemoryBytes(Process process)
    {
        try { return process.PeakWorkingSet64; }
        catch { return 0; }
    }

    private static string ResolveDotNetHost()
    {
        var currentHost = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(currentHost) &&
            Path.GetFileNameWithoutExtension(currentHost).StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return currentHost;
        }

        return "dotnet";
    }

    private void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill timed-out code execution process");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; the OS temp directory will reclaim leftovers if a file is locked.
        }
    }

    private const string RuntimeConfigJson = """
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "framework": {
              "name": "Microsoft.NETCore.App",
              "version": "10.0.0"
            },
            "rollForward": "LatestMajor"
          }
        }
        """;
}
