using Cataben.Domain.Entities;

namespace Cataben.Application.Services;

/// <summary>
/// Runs the post-completion gamification for a submission that has reached a final state:
/// quest progress (all outcomes), then XP/gems, achievements, and notifications (success only).
/// Extracted from the formerly-synchronous SubmitChallengeHandler so the async result consumer
/// (ExecutionResultReceiver) can run the exact same logic once the Worker reports a result.
/// </summary>
public interface ISubmissionCompletionService
{
    Task CompleteAsync(Submission submission, Challenge challenge, CancellationToken cancellationToken = default);
}
