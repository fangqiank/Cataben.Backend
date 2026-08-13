using Cataben.Application.Repositories;
using Cataben.Application.Services;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Shared.Constants;
using Cataben.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cataben.API.BackgroundServices;

/// <summary>
/// Consumes <c>code.result.{executionId}</c> (Core NATS, <c>cataben-results</c> queue group)
/// published by Cataben.Worker after it compiles/runs/tests a submission. Applies the result to
/// the <see cref="Submission"/> (idempotently — skips if already final), then runs gamification via
/// <see cref="ISubmissionCompletionService"/>. Core NATS is at-most-once by design: if no API
/// replica is subscribed when a result is published it is lost, and the submission stays in
/// progress until the user resubmits.
/// </summary>
public class ExecutionResultReceiver : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExecutionResultReceiver> _logger;

    public ExecutionResultReceiver(IMessageBus bus, IServiceProvider services, ILogger<ExecutionResultReceiver> logger)
    {
        _bus = bus;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExecutionResultReceiver subscribing to code.result.> [queue={Queue}]",
            ApplicationConstants.Nats.ResultQueueGroup);

        // SubscribeAsync blocks for the host lifetime (iterates the Core NATS subscription until
        // stoppingToken fires). API replicas share the queue group so each result is handled by
        // exactly one, and the receiver survives restarts because results are re-derived from the
        // (already-persisted) submission state, not from an in-memory queue.
        await _bus.SubscribeAsync<ExecutionResultMessage>(
            "code.result.>",
            ApplicationConstants.Nats.ResultQueueGroup,
            HandleResultAsync,
            stoppingToken);
    }

    private async Task HandleResultAsync(ExecutionResultMessage msg, CancellationToken cancellationToken)
    {
        // Scoped services (DbContext-backed) must NOT be captured into this singleton hosted
        // service — resolve them per message from a fresh scope (avoids captive dependency and
        // keeps each result on its own DbContext change-tracking graph).
        using var scope = _services.CreateScope();
        var submissions = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
        var challenges = scope.ServiceProvider.GetRequiredService<IChallengeRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var completion = scope.ServiceProvider.GetRequiredService<ISubmissionCompletionService>();

        try
        {
            if (msg.SubmissionId == Guid.Empty)
            {
                _logger.LogDebug("Ignoring result {ExecutionId} with no submission (run-code stays synchronous)",
                    msg.ExecutionId);
                return;
            }

            var submission = await submissions.GetByIdAsync(msg.SubmissionId, cancellationToken);
            if (submission == null)
            {
                _logger.LogWarning("Result for unknown submission {SubmissionId}; ignoring", msg.SubmissionId);
                return;
            }

            // Idempotency: at-least-once delivery (and any duplicate publish) must never
            // double-apply test results or double-award XP/achievements.
            if (submission.IsFinal())
            {
                _logger.LogInformation("Submission {SubmissionId} already final ({Status}); ignoring result",
                    msg.SubmissionId, submission.Status);
                return;
            }

            foreach (var tr in msg.TestResults)
            {
                submission.AddTestResult(new Cataben.Domain.Entities.TestResult(
                    tr.Name, tr.Passed, tr.Score, null, null, tr.Message,
                    TimeSpan.FromMilliseconds(tr.ExecutionTimeMs)));
            }

            switch (msg.Status)
            {
                case SubmissionStatus.Completed:
                    submission.MarkAsCompleted(
                        msg.Score, msg.TotalScore, msg.ExecutionTimeMs, msg.MemoryAllocatedBytes, msg.QueryPlan);
                    break;
                case SubmissionStatus.Timeout:
                    submission.MarkAsTimeout();
                    break;
                case SubmissionStatus.SystemError:
                    submission.MarkAsSystemError(msg.Error ?? "System error");
                    break;
                default:
                    // Failed (and any other terminal non-success status the Worker may report).
                    submission.MarkAsFailed(msg.Error ?? "Execution failed");
                    break;
            }

            if (msg.QueryPlan is not null)
                submission.SetQueryInfo(msg.QueryPlan, msg.QueryCost ?? 0);

            await submissions.UpdateAsync(submission);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Gamification: quest progress (all outcomes) + XP/achievements/notifications (success).
            var challengeId = msg.ChallengeId ?? submission.ChallengeId;
            var challenge = await challenges.GetByIdAsync(challengeId, cancellationToken);
            if (challenge != null)
                await completion.CompleteAsync(submission, challenge, cancellationToken);

            _logger.LogInformation("Applied result for submission {SubmissionId}: {Status} ({Score}/{Total})",
                msg.SubmissionId, msg.Status, msg.Score, msg.TotalScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying execution result for {SubmissionId}", msg.SubmissionId);
        }
    }
}
