using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class ChallengeRepository(AppDbContext context) : IChallengeRepository
    {
        public async Task<Challenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .Include(c => c.TestCases)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetAllAsync(
            ChallengeType? type = null,
            string? category = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
            )
        {
            IQueryable<Challenge> query = context.Challenges.AsQueryable();

            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrEmpty(category)) query = query.Where(c => c.Category == category);

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetAllPublicAsync(
            ChallengeType? type = null,
            string? category = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Challenge> query = context.Challenges.Where(c => c.IsActive);

            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrEmpty(category)) query = query.Where(c => c.Category == category);

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<Challenge?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .Include(c => c.TestCases)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetByLearningPathAsync(
            Guid learningPathId,
            CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .Where(c => c.LearningPathId == learningPathId)
                .OrderBy(c => c.OrderInPath)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetPublicByLearningPathAsync(
            Guid learningPathId,
            CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .Where(c => c.LearningPathId == learningPathId && c.IsActive)
                .OrderBy(c => c.OrderInPath)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<Guid, List<Guid>>> GetPublicChallengeIdsByLearningPathAsync(
            IEnumerable<Guid> learningPathIds,
            CancellationToken cancellationToken = default)
        {
            var pathIds = learningPathIds.ToList();
            if (pathIds.Count == 0)
                return new Dictionary<Guid, List<Guid>>();

            var rows = await context.Challenges
                .Where(c => c.IsActive
                    && c.LearningPathId != null
                    && pathIds.Contains(c.LearningPathId.Value))
                .Select(c => new { ChallengeId = c.Id, PathId = c.LearningPathId!.Value })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.PathId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ChallengeId).ToList());
        }

        public async Task<int> GetCountByCategoryAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .CountAsync(c => c.Category == category, cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetChallengesByCategoryAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            return await context.Challenges
                .Where(c => c.Category == category && c.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Challenge challenge, CancellationToken cancellationToken = default)
        {
            await context.Challenges.AddAsync(challenge, cancellationToken);
        }

        public Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken = default)
        {
            context.Challenges.Update(challenge);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var challenge = context.Challenges.Find(id);
            if (challenge != null)
                context.Challenges.Remove(challenge);

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<Challenge>> GetRandomChallengesAsync(
            int count,
            string? category = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Challenges.AsQueryable();
            query = query.Where(c => c.IsActive);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(c => c.Category == category);

            var total = await query.CountAsync(cancellationToken);
            if (total == 0)
                return Enumerable.Empty<Challenge>();

            return await query
                .OrderBy(c => EF.Functions.Random())
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetTotalChallengesAsync(CancellationToken cancellationToken = default)
        {
            return await context.Challenges.CountAsync(cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetAllActiveAsync(
            int limit = 200,
            CancellationToken cancellationToken = default)
        {
            // Stable ordering (CreatedAt) so the deterministic daily-pick index is reproducible across calls.
            return await context.Challenges
                .Where(c => c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Challenge>> GetByIdsAsync(
            IEnumerable<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            // Materialize once — ids may enumerate multiple times and Contains must translate to SQL IN.
            var idList = ids as IList<Guid> ?? ids.ToList();
            return await context.Challenges
                .Where(c => idList.Contains(c.Id))
                .ToListAsync(cancellationToken);
        }
    }
}
