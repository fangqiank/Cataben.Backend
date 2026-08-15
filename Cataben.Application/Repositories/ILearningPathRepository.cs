using Cataben.Domain.Entities;

namespace Cataben.Application.Repositories
{
    public interface ILearningPathRepository
    {
        Task<LearningPath?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<LearningPath?> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<LearningPath>> GetAllAsync(bool onlyPublished = true, CancellationToken cancellationToken = default);
        Task<IEnumerable<LearningPath>> GetByLevelAsync(string level, CancellationToken cancellationToken = default);
        Task<UserLearningPath?> GetUserProgressAsync(Guid userId, Guid learningPathId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserLearningPath>> GetUserProgressAllAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserLearningPath>> GetUserCompletedPathsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task AddAsync(LearningPath learningPath, CancellationToken cancellationToken = default);
        Task UpdateAsync(LearningPath learningPath, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddUserProgressAsync(UserLearningPath progress, CancellationToken cancellationToken = default);
        Task UpdateUserProgressAsync(UserLearningPath progress, CancellationToken cancellationToken = default);

    }
}
