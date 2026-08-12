using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cataben.Application.Handlers
{
    public class SubmitChallengeHandler(
            IChallengeRepository challengeRepository,
            ISubmissionRepository submissionRepository,
            IUserRepository userRepository,
            ICodeExecutor codeExecutor,
            IAchievementService achievementService,
            IQuestService questService,
            IXpTransactionRepository xpTransactionRepository,
            INotificationService notificationService,
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

            var submission = default(Submission);

            try
            {
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

                submission = new Submission(
                    user,
                    challenge,
                    request.Code,
                    attemptNumber,
                    request.UserAgent,
                    request.IpAddress);

                submission.MarkAsCompiling();
                await submissionRepository.AddAsync(submission);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var options = new ExecutionOptions
                {
                    Timeout = TimeSpan.FromSeconds(challenge.TimeLimitSeconds),
                    MaxMemoryBytes = challenge.MemoryLimitMb * 1024 * 1024,
                    EnableDatabase = challenge.Type == ChallengeType.Database,
                    CaptureQueryPlan = true
                };

                submission.MarkAsExecuting();
                await submissionRepository.UpdateStatusAsync(submission);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var parameters = new Dictionary<string, object>
                {
                    ["challenge_id"] = challenge.Id,
                    ["test_cases"] = challenge.TestCases
                };

                var executionResult = await codeExecutor.ExecuteAsync(
                    request.Code,
                    parameters,
                    options,
                    cancellationToken);

                submission.MarkAsTesting();
                await submissionRepository.UpdateStatusAsync(submission);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var testResults = await RunTests(executionResult, challenge);

                foreach (var result in testResults)
                {
                    submission.AddTestResult(new TestResult(
                        result.Name,
                        result.Passed,
                        result.Score,
                        "Expected output",
                        executionResult.Output,
                        result.Message,
                        TimeSpan.FromMilliseconds(result.ExecutionTimeMs)));
                }

                // totalScore = sum of ALL test-case weights (max possible).
                // score = sum of weights for PASSED tests (each TestResultDto.Score is weight-if-passed else 0).
                var totalScore = challenge.TestCases.Sum(t => t.Weight);
                var score = testResults.Sum(t => t.Score);

                if (!string.IsNullOrEmpty(executionResult.Error))
                {
                    submission.MarkAsFailed(executionResult.Error);
                }
                else
                {
                    submission.MarkAsCompleted(
                        score,
                        totalScore,
                        executionResult.ExecutionTimeMs,
                        executionResult.MemoryAllocatedBytes,
                        executionResult.QueryPlan);
                }

                await submissionRepository.UpdateAsync(submission);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                // Best-effort quest progress recompute. QuestService owns its own SaveChanges and swallows
                // all errors internally, so this can never roll back the submission persisted just above.
                // It runs for BOTH outcomes: the Submissions quest metric counts every submission, while
                // SolvedChallenges/DistinctDifficulties filter to IsSuccessful server-side, so a failure
                // advances only the effort-based metric. Absolute recompute reads the row just saved.
                try
                {
                    await questService.CheckAndProgressQuestsAsync(
                        user.Id,
                        new QuestTrigger
                        {
                            WasSuccessful = submission.IsSuccess(),
                            ChallengeId = challenge.Id,
                            ChallengeDifficultyName = challenge.Difficulty.Name
                        },
                        cancellationToken);
                }
                catch (Exception questEx)
                {
                    logger.LogWarning(questEx, "Quest progress update failed for user {UserId}", user.Id);
                }

                if (submission.IsSuccess())
                {
                    user.AddXp(challenge.XpReward);
                    user.AddGems(challenge.GemReward);
                    await xpTransactionRepository.AddAsync(
                        new XpTransaction(user.Id, challenge.XpReward, XpSource.Challenge, challenge.Id.ToString()),
                        cancellationToken);
                    await userRepository.UpdateAsync(user);
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    // Check achievements
                    var trigger = new AchievementTrigger
                    {
                        Type = AchievementType.Count,
                        Value = 1,
                        ChallengeId = challenge.Id,
                        IsPerfect = score == totalScore,
                        Score = score,
                        ExecutionTimeMs = (int)executionResult.ExecutionTimeMs
                    };

                    var unlocked = await achievementService.CheckAndUnlockAchievementsAsync(
                        user.Id, trigger, cancellationToken);

                    // Persist newly unlocked achievements (and any XP/gems they awarded).
                    if (unlocked.Any())
                        await unitOfWork.SaveChangesAsync(cancellationToken);

                    // Send notifications
                    await notificationService.SendChallengeCompletedNotificationAsync(
                        user.Id,
                        MapToSubmissionDto(submission));

                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.SetTag("submission.success", submission.IsSuccess());
                    activity?.SetTag("submission.score", submission.Score);

                    return new SubmissionResultDto
                    {
                        SubmissionId = submission.Id,
                        Status = submission.Status,
                        IsSuccessful = submission.IsSuccess(),
                        Score = submission.Score,
                        TotalScore = submission.TotalScore,
                        ScorePercentage = submission.GetScorePercentage(),
                        ExecutionTimeMs = submission.ExecutionTimeMs,
                        MemoryUsedKB = submission.MemoryUsedBytes / 1024,
                        TestResults = submission.TestResults.Select(t => new TestResultDto
                        {
                            Name = t.Name,
                            Passed = t.Passed,
                            Score = t.Score,
                            Message = t.Message,
                            ExecutionTimeMs = (long)t.ExecutionTime.TotalMilliseconds
                        }).ToList(),
                        ErrorMessage = submission.ErrorMessage,
                        CompletedAt = submission.CompletedAt
                    };
                }

                // Submission did not pass (failed / partial) — return the outcome.
                return new SubmissionResultDto
                {
                    SubmissionId = submission.Id,
                    Status = submission.Status,
                    IsSuccessful = submission.IsSuccess(),
                    Score = submission.Score,
                    TotalScore = submission.TotalScore,
                    ScorePercentage = submission.GetScorePercentage(),
                    ExecutionTimeMs = submission.ExecutionTimeMs,
                    MemoryUsedKB = submission.MemoryUsedBytes / 1024,
                    TestResults = submission.TestResults.Select(t => new TestResultDto
                    {
                        Name = t.Name,
                        Passed = t.Passed,
                        Score = t.Score,
                        Message = t.Message,
                        ExecutionTimeMs = (long)t.ExecutionTime.TotalMilliseconds
                    }).ToList(),
                    ErrorMessage = submission.ErrorMessage,
                    CompletedAt = submission.CompletedAt
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error submitting challenge");

                if (submission != null)
                {
                    submission.MarkAsSystemError(ex.Message);
                    await submissionRepository.UpdateAsync(submission);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }
        }

        private SubmissionDto MapToSubmissionDto(Submission submission)
        {
            return new SubmissionDto
            {
                Id = submission.Id,
                UserId = submission.UserId,
                ChallengeId = submission.ChallengeId,
                Code = submission.Code,
                Status = submission.Status,
                IsSuccessful = submission.IsSuccessful,
                Score = submission.Score,
                TotalScore = submission.TotalScore,
                ExecutionTimeMs = submission.ExecutionTimeMs,
                MemoryUsedBytes = submission.MemoryUsedBytes,
                ErrorMessage = submission.ErrorMessage,
                QueryPlan = submission.QueryPlan,
                SubmittedAt = submission.SubmittedAt,
                CompletedAt = submission.CompletedAt,
                ScorePercentage = submission.GetScorePercentage()
            };
        }

        private async Task<List<TestResultDto>> RunTests(ExecutionResultDto executionResult, Challenge challenge)
        {
            var results = new List<TestResultDto>();

            foreach (var testCase in challenge.TestCases)
            {
                var passed = false;
                var message = string.Empty;

                try
                {
                    var actual = (executionResult.Output ?? string.Empty).Trim();
                    var expected = (testCase.ExpectedOutput ?? string.Empty).Trim();
                    passed = actual.Equals(expected, StringComparison.Ordinal);

                    if (!passed)
                    {
                        message = $"Expected: {testCase.ExpectedOutput}, Got: {executionResult.Output}";
                    }
                }
                catch (Exception ex)
                {
                    message = $"Test error: {ex.Message}";
                    passed = false;
                }

                results.Add(new TestResultDto
                {
                    Name = testCase.Name,
                    Passed = passed,
                    Score = passed ? testCase.Weight : 0,
                    Message = message,
                    ExecutionTimeMs = 10
                });
            }

            return results;
        }
    }
}
