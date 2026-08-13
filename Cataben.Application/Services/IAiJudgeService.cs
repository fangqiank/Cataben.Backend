namespace Cataben.Application.Services;

/// <summary>
/// Semantic code-output judging via an LLM. Used by the Worker's TestRunner for test
/// cases whose <c>ValidationType == "ai"</c>: expected vs actual output are compared for
/// semantic equivalence (not byte equality), tolerating format/layout differences.
/// </summary>
/// <remarks>
/// Implementations MUST be fail-soft: on any error (network, bad key, malformed/escaped
/// response) they return a not-passed result rather than throwing. An <c>ai</c>-mode case
/// can never be allowed to push a submission into SystemError or trigger a NATS poison-loop —
/// it is one test case, and failing it softly is always preferable to crashing the execution.
/// </remarks>
public interface IAiJudgeService
{
    Task<AiJudgeResult> JudgeAsync(
        string expectedOutput,
        string actualOutput,
        string? hint,
        CancellationToken cancellationToken = default);
}

/// <param name="Passed">Whether the actual output is semantically acceptable.</param>
/// <param name="Reason">Human-readable rationale; surfaced to the submitter on failure.</param>
public sealed record AiJudgeResult(bool Passed, string Reason);
