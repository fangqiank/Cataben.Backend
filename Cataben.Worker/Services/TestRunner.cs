using Cataben.Application.Services;
using Cataben.Shared.Messaging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cataben.Worker.Services;

/// <summary>
/// Evaluates compiled code against its test cases. Each case carries its own <see cref="SingleRunResult"/>
/// (the output from feeding that case's Input, or the shared no-stdin run), so cases with different inputs
/// are judged against their OWN output rather than a single shared stdout. Comparison mode is driven
/// per-case by <c>ValidationType</c>: exact | contains | regex | json | loose | ai.
/// </summary>
/// <remarks>
/// Behavior changes vs. the old <c>RunTestsAsync</c>:
/// <list type="bullet">
/// <item>Comparison uses ONLY stdout (was stdout + stderr). stderr now only appears in failure messages.</item>
/// <item>Public cases honor their own <c>ValidationType</c> (were hardcoded to "exact").</item>
/// <item>A run that crashed/timed out fails its case outright (no comparison against partial output).</item>
/// </list>
/// </remarks>
public sealed class TestRunner(IAiJudgeService aiJudge, ILogger<TestRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<List<TestResult>> EvaluateAsync(
        IReadOnlyList<TestCaseEvaluation> cases,
        CancellationToken cancellationToken)
    {
        var results = new List<TestResult>(cases.Count);

        logger.LogInformation("Evaluating {Total} test cases", cases.Count);

        foreach (var c in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A run that didn't complete (crash/timeout/system error) cannot pass, regardless of mode.
            if (!c.Run.Success)
            {
                results.Add(MakeResult(c.Name, passed: false, c.Weight, $"{c.Run.Status}: {c.Run.FailureDetail ?? c.Run.Error}"));
                continue;
            }

            var outcome = await ValidateAsync(
                c.Run.Output ?? string.Empty,
                c.Run.Error,
                c.ExpectedOutput,
                c.ValidationType,
                c.Hint,
                cancellationToken);
            results.Add(MakeResult(c.Name, outcome.Passed, c.Weight, outcome.Message));
        }

        logger.LogInformation("Evaluation complete. Passed: {Passed}/{Total}",
            results.Count(r => r.Passed), results.Count);

        return results;
    }

    private static TestResult MakeResult(string name, bool passed, int weight, string message) => new()
    {
        Name = name,
        Passed = passed,
        Score = passed ? weight : 0, // weight on pass, 0 otherwise — sum of these IS the achieved score
        Message = message,
        ExecutionTimeMs = 0,
        MemoryUsedBytes = 0
    };

    private async Task<ValidationOutcome> ValidateAsync(
        string output,
        string? runError,
        string expectedRaw,
        string validationType,
        string? hint,
        CancellationToken cancellationToken)
    {
        var expected = (expectedRaw ?? string.Empty).Trim();
        var actual = output.Trim();
        var mode = (validationType ?? "exact").ToLowerInvariant();

        switch (mode)
        {
            case "contains":
                if (!string.IsNullOrEmpty(expected) && actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
                    return new(true, "Passed (contains)");
                return new(false, $"Expected to contain: '{expected}', got: '{actual}'{ErrorSuffix(runError)}");

            case "regex":
                try
                {
                    if (new Regex(expected, RegexOptions.Compiled | RegexOptions.IgnoreCase).IsMatch(actual))
                        return new(true, "Passed (regex)");
                    return new(false, $"Regex '{expected}' did not match output");
                }
                catch (Exception ex)
                {
                    return new(false, $"Invalid regex: {ex.Message}");
                }

            case "json":
                try
                {
                    var e = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(expected), JsonOptions);
                    var a = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(actual), JsonOptions);
                    return string.Equals(e, a, StringComparison.Ordinal)
                        ? new(true, "Passed (json)")
                        : new(false, "JSON mismatch");
                }
                catch (Exception ex)
                {
                    return new(false, $"Invalid JSON: {ex.Message}");
                }

            case "loose":
            {
                // Ignore ALL whitespace (incl. internal) and case — "1 2 3" == "  1  2  3 ".
                var exp = RemoveAllWhitespace(expected);
                var act = RemoveAllWhitespace(actual);
                return string.Equals(exp, act, StringComparison.OrdinalIgnoreCase)
                    ? new(true, "Passed (loose)")
                    : new(false, $"Loose mismatch: expected '{exp}', got '{act}'");
            }

            case "ai":
            {
                // Semantic equivalence via the LLM; fail-soft (never throws here).
                var r = await aiJudge.JudgeAsync(expectedRaw ?? string.Empty, output, hint, cancellationToken);
                return new(r.Passed, r.Passed ? "Passed (ai)" : $"AI: {r.Reason}");
            }

            default: // exact
                return string.Equals(expected, actual, StringComparison.Ordinal)
                    ? new(true, "Passed")
                    : new(false, $"Expected: '{expected}', got: '{actual}'{ErrorSuffix(runError)}");
        }
    }

    private static string ErrorSuffix(string? runError) =>
        string.IsNullOrWhiteSpace(runError) ? string.Empty : $" (stderr: {runError.Trim()})";

    private static string RemoveAllWhitespace(string s) =>
        string.IsNullOrEmpty(s) ? string.Empty : string.Concat(s.Where(c => !char.IsWhiteSpace(c)));

    private sealed record ValidationOutcome(bool Passed, string Message);
}
