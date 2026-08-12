using Cataben.Application.DTOs;

namespace Cataben.Application.Services
{
    public interface INotificationService
    {
        Task SendAchievementNotificationAsync(Guid userId, UserAchievementDto achievement);
        Task SendChallengeCompletedNotificationAsync(Guid userId, SubmissionDto submission);
        Task SendEmailAsync(string to, string subject, string body);
        Task SendLevelUpNotificationAsync(Guid userId, int newLevel);
        Task SendStreakNotificationAsync(Guid userId, int streak);
    }
}
