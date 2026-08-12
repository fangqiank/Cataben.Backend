using Cataben.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Services
{
    public class NotificationService(ILogger<NotificationService> logger) : INotificationService
    {
        public Task SendAchievementNotificationAsync(Guid userId, UserAchievementDto achievement)
        {
            logger.LogInformation("Achievement unlocked for user {UserId}: {Achievement}",
                userId, achievement.Name);

            return Task.CompletedTask;
        }

        public Task SendChallengeCompletedNotificationAsync(Guid userId, SubmissionDto submission)
        {
            logger.LogInformation("Challenge completed by user {UserId}: {SubmissionId}",
                userId, submission.Id);

            return Task.CompletedTask;
        }

        public Task SendLevelUpNotificationAsync(Guid userId, int newLevel)
        {
            logger.LogInformation("User {UserId} reached level {Level}", userId, newLevel);

            return Task.CompletedTask;
        }

        public Task SendStreakNotificationAsync(Guid userId, int streak)
        {
            logger.LogInformation("User {UserId} reached {Streak} day streak", userId, streak);

            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string to, string subject, string body)
        {
            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);

            return Task.CompletedTask;
        }
    }
}
