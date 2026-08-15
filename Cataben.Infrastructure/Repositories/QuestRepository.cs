using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class QuestRepository(AppDbContext context) : IQuestRepository
    {
        public async Task<IEnumerable<Quest>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await context.Quests
                .Where(q => q.IsActive)
                .OrderBy(q => q.Order)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Quest>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await context.Quests
                .OrderBy(q => q.Order)
                .ToListAsync(cancellationToken);
        }

        public async Task<Quest?> GetByIdAsync(string questId, CancellationToken cancellationToken = default)
        {
            return await context.Quests.FirstOrDefaultAsync(q => q.Id == questId, cancellationToken);
        }

        public async Task AddAsync(Quest quest, CancellationToken cancellationToken = default)
        {
            await context.Quests.AddAsync(quest, cancellationToken);
        }

        public async Task<UserQuest?> GetByWindowAsync(
            Guid userId,
            string questId,
            DateTime windowStart,
            CancellationToken cancellationToken = default)
        {
            return await context.UserQuests
                .Include(uq => uq.Quest)
                .Include(uq => uq.User)
                .FirstOrDefaultAsync(uq => uq.UserId == userId
                    && uq.QuestId == questId
                    && uq.WindowStart == windowStart, cancellationToken);
        }

        public async Task<UserQuest?> GetByIdAsync(Guid userQuestId, CancellationToken cancellationToken = default)
        {
            return await context.UserQuests
                .Include(uq => uq.Quest)
                .Include(uq => uq.User)
                .FirstOrDefaultAsync(uq => uq.Id == userQuestId, cancellationToken);
        }

        public async Task<UserQuest?> GetByIdNoTrackingAsync(Guid userQuestId, CancellationToken cancellationToken = default)
        {
            return await context.UserQuests
                .AsNoTracking()
                .Include(uq => uq.Quest)
                .Include(uq => uq.User)
                .FirstOrDefaultAsync(uq => uq.Id == userQuestId, cancellationToken);
        }

        public async Task<IEnumerable<UserQuest>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.UserQuests
                .Include(uq => uq.Quest)
                .Where(uq => uq.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(UserQuest userQuest, CancellationToken cancellationToken = default)
        {
            await context.UserQuests.AddAsync(userQuest, cancellationToken);
        }

        public Task UpdateAsync(UserQuest userQuest, CancellationToken cancellationToken = default)
        {
            context.UserQuests.Update(userQuest);
            return Task.CompletedTask;
        }

        public async Task<bool> TryClaimAsync(
            Guid userQuestId,
            Guid userId,
            DateTime claimedAt,
            CancellationToken cancellationToken = default)
        {
            var updated = await context.UserQuests
                .Where(uq => uq.Id == userQuestId
                    && uq.UserId == userId
                    && uq.IsCompleted
                    && !uq.IsClaimed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(uq => uq.IsClaimed, true)
                        .SetProperty(uq => uq.ClaimedAt, claimedAt),
                    cancellationToken);

            return updated > 0;
        }
    }
}
