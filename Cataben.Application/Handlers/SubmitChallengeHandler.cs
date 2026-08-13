using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using Cataben.Shared.Constants;
using Cataben.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cataben.Application.Handlers
{
    /// <summary>
    /// Async submission entry point: validates, creates the <see cref="Domain.Entities.Submission"/>
    /// (Compiling → Executing), then publishes a durable <c>code.execute</c> message (JetStream —
    /// survives Worker crash + NATS restart) and returns immediately with the pending submission.
    /// Cataben.Worker compiles/runs/tests/scores asynchronously and publishes
    /// <c>code.result.{id}</c> (Core NATS), which <c>ExecutionResultReceiver</c> consumes to
    /// persist the outcome and run gamification. Clients poll <c>GET /submission/{id}</c>.
    /// </summary>
    public class SubmitChallengeHandler(
        IChallengeRepository challengeRepository,
        ISubmissionRepository submissionRepository,
        IUserRepository userRepository,
        IMessageBus messageBus,
        IUnitOfWork unitOfWork,
        IDistributedTracing tracing,
        ILogger<SubmitChallengeHandler> logger
    ) : IRequestHandler<SubmitChallengeCommand, SubmissionResultDto>
    {
        public async Task<SubmissionResultDto> Handle(SubmitChallengeCommand request, CancellationToken cancellationToken)
        {
            using var activity = tracing.StartActivity("SubmitChallenge");
            activity?.SetTag("user.id", request.UserId);
            activity?.SetTag("challenge.id", request.ChallengeId);

            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var challenge = await challengeRepository.GetByIdAsync(request.ChallengeId, cancellationToken);
            if (challenge == null)
                throw new NotFoundException("Challenge not found");

            var existing = await submissionRepository.GetUserSubmissionForChallenge(
                request.UserId, request.ChallengeId, cancellationToken);
            if (existing > 0)
                throw new ValidationException("Challenge already solved");

            var attemptNumber = await submissionRepository.GetAttemptCountForChallenge(
                request.UserId, request.ChallengeId, cancellationToken) + 1;

            var submission = new Submission(
                user,
                challenge,
                request.Code,
                attemptNumber,
                request.UserAgent,
                request.IpAddress);

            submission.MarkAsCompiling();
            await submissionRepository.AddAsync(submission);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            submission.MarkAsExecuting();
            await submissionRepository.UpdateStatusAsync(submission);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Build the execution contract. TestCases + HiddenTests are both mapped now (hidden tests
            // were previously dropped, so the Worker never scored them); each carries its ValidationType
            // so the Worker compares per-case (exact | contains | regex | json | loose | ai). ChallengeType
            // tells the Worker whether to feed per-case stdin (only Algorithm honors the Input field).
            var message = new ExecutionMessage
            {
                ExecutionId = submission.Id.ToString(),
                Code = request.Code,
                ChallengeId = challenge.Id,
                UserId = user.Id,
                SubmissionId = submission.Id,
                TimeoutSeconds = challenge.TimeLimitSeconds,
                MemoryLimitMb = challenge.MemoryLimitMb,
                ChallengeType = challenge.Type,
                HasTests = challenge.TestCases.Any() || challenge.HiddenTests.Any(),
                TestCases = challenge.TestCases.Select(t => new Cataben.Shared.Messaging.TestCaseDto
                {
                    Name = t.Name,
                    Input = t.Input,
                    ExpectedOutput = t.ExpectedOutput,
                    IsPublic = t.IsPublic,
                    Weight = t.Weight,
                    ValidationType = t.ValidationType
                }).ToList(),
                HiddenTests = challenge.HiddenTests.Select(h => new HiddenTestDto
                {
                    Name = h.Name,
                    Input = h.Input,
                    ExpectedOutput = h.ExpectedOutput,
                    ValidationType = h.ValidationType,
                    Weight = h.Weight,
                    MinScore = h.MinScore
                }).ToList(),
                Parameters = new Dictionary<string, object>
                {
                    ["challenge_id"] = challenge.Id,
                    ["test_cases"] = challenge.TestCases
                }
            };

            try
            {
                await messageBus.PublishDurableAsync(
                    ApplicationConstants.QueueNames.CodeExecution, message, cancellationToken);
            }
            catch (Exception pubEx)
            {
                // NATS unavailable: the submission would otherwise sit in Executing forever.
                // Move it to a terminal SystemError so the client knows to retry.
                logger.LogError(pubEx, "Failed to publish execution for submission {SubmissionId}", submission.Id);
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("exception.message", pubEx.Message);

                submission.MarkAsSystemError("Failed to queue execution: " + pubEx.Message);
                await submissionRepository.UpdateAsync(submission);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                throw;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("submission.queued", true);

            // Pending ack — the real result arrives via code.result.{id}; clients poll by id.
            return new SubmissionResultDto
            {
                SubmissionId = submission.Id,
                Status = submission.Status,
                IsSuccessful = submission.IsSuccessful,
                Score = submission.Score,
                TotalScore = submission.TotalScore,
                ScorePercentage = submission.GetScorePercentage(),
                ExecutionTimeMs = submission.ExecutionTimeMs,
                MemoryUsedKB = submission.MemoryUsedBytes / 1024,
                TestResults = new List<TestResultDto>(),
                ErrorMessage = submission.ErrorMessage,
                CompletedAt = submission.CompletedAt
            };
        }
    }
}
