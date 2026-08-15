using Cataben.Domain.Entities;

namespace Cataben.Application.Repositories
{
    public interface IQuestRepository
    {
        /// <summary>All active quest definitions, ordered by <see cref="Quest.Order"/>.</summary>
        Task<IEnumerable<Quest>> GetActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>All quest definitions (incl. inactive), ordered by Order. Admin catalog list.</summary>
        Task<IEnumerable<Quest>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>A single quest definition by its string id.</summary>
        Task<Quest?> GetByIdAsync(string questId, CancellationToken cancellationToken = default);

        /// <summary>Add a new quest definition (catalog).</summary>
        Task AddAsync(Quest quest, CancellationToken cancellationToken = default);

        /// <summary>A user's quest progress for a specific window (Include Quest + User).</summary>
        Task<UserQuest?> GetByWindowAsync(Guid userId, string questId, DateTime windowStart, CancellationToken cancellationToken = default);

        /// <summary>A single UserQuest by id (Include Quest + User).</summary>
        Task<UserQuest?> GetByIdAsync(Guid userQuestId, CancellationToken cancellationToken = default);

        /// <summary>A single UserQuest by id without change tracking, for race-safe claim validation.</summary>
        Task<UserQuest?> GetByIdNoTrackingAsync(Guid userQuestId, CancellationToken cancellationToken = default);

        Task<IEnumerable<UserQuest>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        Task AddAsync(UserQuest userQuest, CancellationToken cancellationToken = default);

        /// <summary>Atomically claims a completed, unclaimed quest. Returns false when already claimed or invalid.</summary>
        Task<bool> TryClaimAsync(
            Guid userQuestId,
            Guid userId,
            DateTime claimedAt,
            CancellationToken cancellationToken = default);

        Task UpdateAsync(UserQuest userQuest, CancellationToken cancellationToken = default);
    }
}
