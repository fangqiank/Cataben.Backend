using Cataben.Domain.Entities;
using Cataben.Domain.Enums;

namespace Cataben.Application.Repositories
{
    public interface IChallengeRepository
    {
        Task AddAsync(Challenge challenge, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Challenge>> GetAllAsync(ChallengeType? type = null, string? category = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<Challenge>> GetAllPublicAsync(ChallengeType? type = null, string? category = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<Challenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Challenge?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Challenge>> GetByLearningPathAsync(Guid learningPathId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Challenge>> GetPublicByLearningPathAsync(Guid learningPathId, CancellationToken cancellationToken = default);
        Task<Dictionary<Guid, List<Guid>>> GetPublicChallengeIdsByLearningPathAsync(IEnumerable<Guid> learningPathIds, CancellationToken cancellationToken = default);
        Task<IEnumerable<Challenge>> GetChallengesByCategoryAsync(string category, CancellationToken cancellationToken = default);
        Task<int> GetCountByCategoryAsync(string category, CancellationToken cancellationToken = default);
        Task<int> GetTotalChallengesAsync(CancellationToken cancellationToken = default);
        Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken = default);
        // Active challenges only (IsActive), for the daily-challenge picker.
        Task<IEnumerable<Challenge>> GetAllActiveAsync(int limit = 200, CancellationToken cancellationToken = default);
        // Batch fetch by ids (for quest DistinctDifficulties resolution).
        Task<IEnumerable<Challenge>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    }
}
