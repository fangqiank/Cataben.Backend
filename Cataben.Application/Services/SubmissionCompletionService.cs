using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cataben.Application.Services;

/// <inheritdoc />
public class SubmissionCompletionService(
    IUserRepository userRepository,
    IXpTransactionRepository xpTransactionRepository,
    IAchievementService achievementService,
    IQuestService questService,
    INotificationService notificationService,
    IUnitOfWork unitOfWork,
    ILogger<SubmissionCompletionService> logger
) : ISubmissionCompletionService
{
    public async Task CompleteAsync(Submission submission, Challenge challenge, CancellationToken cancellationToken = default)
    {
        // Best-effort quest progress recompute. QuestService owns its own SaveChanges and
        // swallows all errors internally, so this can never roll back the caller's work. Runs
        // for BOTH outcomes: the Submissions metric counts every submission, while
        // SolvedChallenges/DistinctDifficulties filter to IsSuccessful server-side.
        try
        {
            await questService.CheckAndProgressQuestsAsync(
                submission.UserId,
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
            logger.LogWarning(questEx, "Quest progress update failed for user {UserId}", submission.UserId);
        }

        if (!submission.IsSuccess())
            return;

        await userRepository.LockByIdAsync(submission.UserId, cancellationToken);
        var user = await userRepository.GetByIdAsync(submission.UserId, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found during submission completion; skipping rewards", submission.UserId);
            return;
        }

        var alreadyRewarded = await xpTransactionRepository.ExistsAsync(
            user.Id,
            XpSource.Challenge,
            challenge.Id.ToString(),
            cancellationToken);
        if (!alreadyRewarded)
        {
            user.AddXp(challenge.XpReward);
            user.AddGems(challenge.GemReward);
            await xpTransactionRepository.AddAsync(
                new XpTransaction(user.Id, challenge.XpReward, XpSource.Challenge, challenge.Id.ToString()),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var trigger = new AchievementTrigger
        {
            Type = AchievementType.Count,
            Value = 1,
            ChallengeId = challenge.Id,
            IsPerfect = submission.Score == submission.TotalScore,
            Score = submission.Score,
            ExecutionTimeMs = (int)submission.ExecutionTimeMs
        };

        await achievementService.CheckAndUnlockAchievementsAsync(user.Id, trigger, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.SendChallengeCompletedNotificationAsync(user.Id, MapToSubmissionDto(submission));
    }

    private static SubmissionDto MapToSubmissionDto(Submission submission) => new()
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
