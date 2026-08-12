using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    /// <summary>
    /// A user's progress on a <see cref="Quest"/> for ONE cadence window (daily = UTC midnight→midnight,
    /// weekly = Monday UTC 00:00→+7d). One row per (User, Quest, WindowStart) — the unique index makes
    /// each new window a fresh row, so "reset" is free and history is preserved (lazy window roll-forward,
    /// no background job needed).
    /// Completion and reward are DECOUPLED: crossing <see cref="Quest.Threshold"/> sets IsCompleted (no XP);
    /// an explicit <see cref="Claim"/> awards XP/gems idempotently. Progress is set via absolute recompute
    /// (see QuestService), so UpdateProgress is monotonic + idempotent under retries.
    /// </summary>
    public class UserQuest
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; } = null!;
        public string QuestId { get; private set; } = string.Empty;
        public Quest Quest { get; private set; } = null!;
        public DateTime WindowStart { get; private set; }
        public DateTime WindowEnd { get; private set; }
        public int Progress { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsClaimed { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; } = new();

        private UserQuest() { }

        public UserQuest(User user, Quest quest, DateTime windowStart)
        {
            Id = Guid.NewGuid();
            UserId = user.Id;
            User = user;
            QuestId = quest.Id;
            Quest = quest;
            WindowStart = windowStart;
            WindowEnd = windowStart.AddDays(quest.Cadence == QuestCadence.Daily ? 1 : 7);
            Progress = 0;
            IsCompleted = false;
            IsClaimed = false;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>Monotonic, idempotent — mirrors <see cref="UserAchievement.UpdateProgress"/>.</summary>
        public void UpdateProgress(int progress)
        {
            Progress = Math.Min(progress, Quest.Threshold);
            if (Progress >= Quest.Threshold && !IsCompleted)
            {
                MarkCompleted();
            }
        }

        /// <summary>Idempotent; sets completion only — does NOT award rewards.</summary>
        public void MarkCompleted()
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Awards XP/gems once. Idempotent: returns false (no-op) if not completed yet or already claimed.
        /// </summary>
        public bool Claim()
        {
            if (!IsCompleted || IsClaimed)
                return false;

            IsClaimed = true;
            ClaimedAt = DateTime.UtcNow;

            if (Quest.XpReward > 0)
                User.AddXp(Quest.XpReward);

            if (Quest.GemReward > 0)
                User.AddGems(Quest.GemReward);

            return true;
        }
    }
}
