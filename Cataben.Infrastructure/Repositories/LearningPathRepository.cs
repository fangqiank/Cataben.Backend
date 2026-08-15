using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Repositories
{
    public class LearningPathRepository(
        AppDbContext context,
        ILogger<LearningPathRepository> logger
        ) : ILearningPathRepository
    {
        public async Task<LearningPath?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.LearningPaths
                    .Include(lp => lp.Challenges)
                        .ThenInclude(c => c.TestCases)
                    .Include(lp => lp.Challenges)
                        .ThenInclude(c => c.HiddenTests)
                    .FirstOrDefaultAsync(lp => lp.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting learning path by id {Id}", id);
                throw;
            }
        }

        public async Task<LearningPath?> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.LearningPaths
                    .Include(lp => lp.Challenges)
                        .ThenInclude(c => c.TestCases)
                    .Include(lp => lp.Challenges)
                        .ThenInclude(c => c.HiddenTests)
                    .FirstOrDefaultAsync(lp => lp.Id == id && lp.IsPublished, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting published learning path by id {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<LearningPath>> GetAllAsync(bool onlyPublished = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = context.LearningPaths
                    .AsQueryable();

                if (onlyPublished)
                {
                    query = query.Where(lp => lp.IsPublished);
                }

                return await query
                    .OrderBy(lp => lp.Order)
                    .ThenBy(lp => lp.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all learning paths");
                throw;
            }
        }

        public async Task<IEnumerable<LearningPath>> GetByLevelAsync(string level, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.LearningPaths
                    .Include(lp => lp.Challenges)
                    .Where(lp => lp.Level == level && lp.IsPublished)
                    .OrderBy(lp => lp.Order)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting learning paths by level {Level}", level);
                throw;
            }
        }

        public async Task<UserLearningPath?> GetUserProgressAsync(Guid userId, Guid learningPathId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.UserLearningPaths
                    .Include(ulp => ulp.LearningPath)
                    .FirstOrDefaultAsync(ulp => ulp.UserId == userId && ulp.LearningPathId == learningPathId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting user progress for user {UserId} and path {LearningPathId}", userId, learningPathId);
                throw;
            }
        }

        public async Task<IEnumerable<UserLearningPath>> GetUserProgressAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.UserLearningPaths
                    .Include(ulp => ulp.LearningPath)
                    .Where(ulp => ulp.UserId == userId)
                    .OrderBy(ulp => ulp.StartedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all user progress for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserLearningPath>> GetUserCompletedPathsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.UserLearningPaths
                    .Include(ulp => ulp.LearningPath)
                    .Where(ulp => ulp.UserId == userId && ulp.IsCompleted)
                    .OrderByDescending(ulp => ulp.CompletedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting completed paths for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUserCompletedPathsCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.UserLearningPaths
                    .CountAsync(ulp => ulp.UserId == userId && ulp.IsCompleted, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting completed paths count for user {UserId}", userId);
                throw;
            }
        }

        public async Task<LearningPathStatistics> GetStatisticsAsync(Guid learningPathId, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = await GetByIdAsync(learningPathId, cancellationToken);
                if (path == null)
                    return new LearningPathStatistics();

                var userProgress = await context.UserLearningPaths
                    .Where(ulp => ulp.LearningPathId == learningPathId)
                    .ToListAsync(cancellationToken);

                return new LearningPathStatistics
                {
                    TotalUsersStarted = userProgress.Count,
                    TotalUsersCompleted = userProgress.Count(ulp => ulp.IsCompleted),
                    AverageProgress = userProgress.Any() ? userProgress.Average(ulp => ulp.Progress) : 0,
                    TotalChallenges = path.Challenges.Count,
                    CompletionRate = userProgress.Any()
                        ? (double)userProgress.Count(ulp => ulp.IsCompleted) / userProgress.Count * 100
                        : 0
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting statistics for learning path {LearningPathId}", learningPathId);
                throw;
            }
        }

        public async Task AddAsync(LearningPath learningPath, CancellationToken cancellationToken = default)
        {
            try
            {
                await context.LearningPaths.AddAsync(learningPath, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding learning path");
                throw;
            }
        }

        public async Task UpdateAsync(LearningPath learningPath, CancellationToken cancellationToken = default)
        {
            try
            {
                context.LearningPaths.Update(learningPath);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating learning path {Id}", learningPath.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var path = await GetByIdAsync(id, cancellationToken);
                if (path != null)
                {
                    // Remove all user progress first
                    var userProgress = await context.UserLearningPaths
                        .Where(ulp => ulp.LearningPathId == id)
                        .ToListAsync(cancellationToken);

                    context.UserLearningPaths.RemoveRange(userProgress);

                    // Remove the learning path
                    context.LearningPaths.Remove(path);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting learning path {Id}", id);
                throw;
            }
        }

        public async Task AddUserProgressAsync(UserLearningPath progress, CancellationToken cancellationToken = default)
        {
            try
            {
                await context.UserLearningPaths.AddAsync(progress, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding user progress");
                throw;
            }
        }

        public async Task UpdateUserProgressAsync(UserLearningPath progress, CancellationToken cancellationToken = default)
        {
            try
            {
                // Calculate progress based on completed challenges
                var path = await GetByIdAsync(progress.LearningPathId, cancellationToken);
                if (path != null)
                {
                    // Get completed challenges count from submissions
                    var completedCount = await context.Submissions
                        .Where(s => s.UserId == progress.UserId
                            && s.IsSuccessful
                            && path.Challenges.Select(c => c.Id).Contains(s.ChallengeId))
                        .Select(s => s.ChallengeId)
                        .Distinct()
                        .CountAsync(cancellationToken);

                    progress.UpdateProgress(completedCount);
                }

                context.UserLearningPaths.Update(progress);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating user progress");
                throw;
            }
        }

        public async Task<bool> IsChallengeInPathAsync(Guid challengeId, Guid learningPathId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.Challenges
                    .AnyAsync(c => c.Id == challengeId && c.LearningPathId == learningPathId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking if challenge {ChallengeId} is in path {LearningPathId}", challengeId, learningPathId);
                throw;
            }
        }

        public async Task<Dictionary<Guid, int>> GetUserProgressForPathsAsync(Guid userId, IEnumerable<Guid> pathIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var progressDict = new Dictionary<Guid, int>();

                foreach (var pathId in pathIds)
                {
                    var progress = await GetUserProgressAsync(userId, pathId, cancellationToken);
                    progressDict[pathId] = progress?.Progress ?? 0;
                }

                return progressDict;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting user progress for multiple paths");
                throw;
            }
        }
    }

    public class LearningPathStatistics
    {
        public int TotalUsersStarted { get; set; }
        public int TotalUsersCompleted { get; set; }
        public double AverageProgress { get; set; }
        public int TotalChallenges { get; set; }
        public double CompletionRate { get; set; }
    }

}
