namespace Cataben.Worker.Services;

/// <summary>
/// A single test case flattened for evaluation, with the run that produced its output already attached.
/// Public and hidden cases normalize into this shape so the evaluator treats them identically; each case
/// carries the <see cref="Run"/> that corresponds to its own <see cref="Input"/> (or the shared no-stdin run
/// when the case has no Input / the challenge isn't an Algorithm challenge).
/// </summary>
public sealed class TestCaseEvaluation
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The stdin this case expects to be fed (only honored for Algorithm challenges).</summary>
    public string? Input { get; set; }

    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>Comparison mode: exact | contains | regex | json | loose | ai.</summary>
    public string ValidationType { get; set; } = "exact";

    public int Weight { get; set; } = 1;

    /// <summary>Optional guidance for AI-mode judging (e.g. "accept any ordering").</summary>
    public string? Hint { get; set; }

    /// <summary>The execution result that produced this case's output.</summary>
    public required SingleRunResult Run { get; set; }
}
