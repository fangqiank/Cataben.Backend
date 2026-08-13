using Cataben.Application.Services;

namespace Cataben.Worker.Services;

/// <summary>
/// Stand-in <see cref="IAiJudgeService"/> used when no AI key is configured. Every AI-mode test
/// case is marked not-passed with an explanatory reason instead of throwing — the platform stays up
/// and the rest of the submission still judges normally. Prevents a missing <c>Ai:ApiKey</c> from
/// crashing execution or poisoning the queue.
/// </summary>
public sealed class NoOpAiJudgeService : IAiJudgeService
{
    public Task<AiJudgeResult> JudgeAsync(
        string expectedOutput,
        string actualOutput,
        string? hint,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AiJudgeResult(false, "AI judging is not configured (set Ai:ApiKey)"));
}
