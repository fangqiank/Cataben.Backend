using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    /// <summary>
    /// A repeating task definition (daily/weekly). Progress is tracked per cadence-window via
    /// <see cref="UserQuest"/> rows. Modeled after <see cref="Achievement"/> (string PK, rich entity).
    /// Rewards are explicit (not derived from rarity), since quests are tuned individually.
    /// </summary>
    public class Quest
    {
        public string Id { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public QuestCadence Cadence { get; private set; }
        public QuestMetric Metric { get; private set; }
        public int Threshold { get; private set; }
        public int XpReward { get; private set; }
        public int GemReward { get; private set; }
        public string Icon { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public int Order { get; private set; }
        public Dictionary<string, object> Criteria { get; private set; } = new();

        private Quest() { }

        public Quest(
            string id,
            string name,
            string description,
            QuestCadence cadence,
            QuestMetric metric,
            int threshold,
            int xpReward,
            int gemReward,
            string icon = "🎯",
            int order = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            Cadence = cadence;
            Metric = metric;
            Threshold = threshold;
            XpReward = xpReward;
            GemReward = gemReward;
            Icon = icon;
            IsActive = true;
            Order = order;
        }

        public void SetActive(bool value) => IsActive = value;
    }
}
