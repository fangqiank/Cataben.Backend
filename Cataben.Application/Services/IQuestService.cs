using Cataben.Application.DTOs;
using Cataben.Domain.Entities;

namespace Cataben.Application.Services
{
    public interface IQuestService
    {
        /// <summary>The current user's active quests with their CURRENT-window progress.
        /// Quests with no UserQuest row yet are returned as virtual DTOs (progress 0) so the
        /// frontend always sees all active quests.</summary>
        Task<IEnumerable<UserQuestDto>> GetActiveUserQuestsAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Recompute absolute window progress for every active quest and persist deltas.
        /// Persists its own changes (best-effort: any failure — incl. a concurrent first-row race on
        /// the unique index — is logged and self-heals via absolute recompute on the next submission).
        /// Returns the rows that changed.
        /// Idempotent under retries (absolute recompute, not relative increment).</summary>
        Task<IEnumerable<UserQuest>> CheckAndProgressQuestsAsync(Guid userId, QuestTrigger trigger, CancellationToken cancellationToken = default);

        /// <summary>Claim the reward for a completed quest (idempotent). Returns null if the
        /// UserQuest does not exist or belongs to another user; throws ValidationException if
        /// not yet completed.</summary>
        Task<UserQuestDto?> ClaimRewardAsync(Guid userId, Guid userQuestId, CancellationToken cancellationToken = default);
    }

    /// <summary>Carries the facts about a submission needed to recompute quest progress.
    /// Co-located with <see cref="IQuestService"/> (mirrors <see cref="AchievementTrigger"/>).</summary>
    public class QuestTrigger
    {
        public bool WasSuccessful { get; set; }
        public Guid? ChallengeId { get; set; }
        public string? ChallengeDifficultyName { get; set; }
    }
}
