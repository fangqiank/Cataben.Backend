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
/// Consumes <c>code.result.{executionId}</c> (JetStream durable) published by Cataben.Worker after
/// it compiles/runs/tests a submission. Applies the result to the <see cref="Submission"/>
/// (idempotently — skips if already final), then runs gamification via
/// <see cref="ISubmissionCompletionService"/>. Messages are HMAC-signed by the Worker so API
/// replicas reject forged results even if an unauthorized client reaches NATS.
/// </summary>
public class ExecutionResultReceiver : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExecutionResultReceiver> _logger;
    private readonly string _resultSigningKey;

    public ExecutionResultReceiver(
        IMessageBus bus,
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<ExecutionResultReceiver> logger)
    {
        _bus = bus;
        _services = services;
        _logger = logger;
        _resultSigningKey = configuration["Nats:ResultSigningKey"] ?? string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExecutionResultReceiver subscribing to code.result.> [consumer={Consumer}]",
            ApplicationConstants.Nats.ResultsDurableConsumer);

        await _bus.SubscribeDurableAsync<ExecutionResultMessage>(
            ApplicationConstants.Nats.ResultsStreamSubject,
            ApplicationConstants.Nats.ResultsDurableConsumer,
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
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var completion = scope.ServiceProvider.GetRequiredService<ISubmissionCompletionService>();

        try
        {
            if (!ExecutionResultSigner.TryVerify(msg, _resultSigningKey))
            {
                _logger.LogWarning("Ignoring unsigned or invalid execution result for {ExecutionId}", msg.ExecutionId);
                return;
            }

            if (msg.SubmissionId == Guid.Empty)
            {
                _logger.LogDebug("Ignoring result {ExecutionId} with no submission (run-code stays synchronous)",
                    msg.ExecutionId);
                return;
            }

            // One transaction for result application + gamification. Runs via the execution
            // strategy (retried as a whole on transient failure — safe because the row locks
            // and IsFinal()/already-solved guards below make the block re-runnable).
            await unitOfWork.ExecuteTransactionAsync(async ct =>
            {
                await submissions.LockByIdAsync(msg.SubmissionId, ct);

                var submission = await submissions.GetByIdAsync(msg.SubmissionId, ct);
                if (submission == null)
                {
                    _logger.LogWarning("Result for unknown submission {SubmissionId}; ignoring", msg.SubmissionId);
                    return;
                }

                // Idempotency: the row lock serializes duplicate result delivery, so only one
                // receiver can move this submission from in-progress to final.
                if (submission.IsFinal())
                {
                    _logger.LogInformation("Submission {SubmissionId} already final ({Status}); ignoring result",
                        msg.SubmissionId, submission.Status);
                    return;
                }

                var challengeId = msg.ChallengeId ?? submission.ChallengeId;
                if (msg.Status == SubmissionStatus.Completed)
                {
                    await users.LockByIdAsync(submission.UserId, ct);
                    var alreadySolved = await submissions.GetUserSubmissionForChallenge(
                        submission.UserId,
                        challengeId,
                        ct);
                    if (alreadySolved > 0)
                    {
                        submission.MarkAsFailed("Challenge already solved");
                        await submissions.UpdateAsync(submission);
                        await unitOfWork.SaveChangesAsync(ct);
                        _logger.LogInformation(
                            "Submission {SubmissionId} was marked failed because challenge {ChallengeId} is already solved",
                            msg.SubmissionId,
                            challengeId);
                        return;
                    }
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

                // Gamification: quest progress (all outcomes) + XP/achievements/notifications (success).
                var challenge = await challenges.GetByIdAsync(challengeId, ct);
                if (challenge != null)
                    await completion.CompleteAsync(submission, challenge, ct);

                await unitOfWork.SaveChangesAsync(ct);

                _logger.LogInformation("Applied result for submission {SubmissionId}: {Status} ({Score}/{Total})",
                    msg.SubmissionId, msg.Status, msg.Score, msg.TotalScore);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying execution result for {SubmissionId}", msg.SubmissionId);
            throw;
        }
    }
}
