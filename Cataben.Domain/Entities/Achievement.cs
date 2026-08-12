using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    public class Achievement
    {
        public string Id { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public AchievementCategory Category { get; private set; }
        public AchievementRarity Rarity { get; private set; }
        public int RequiredProgress { get; private set; }
        public int XpReward { get; private set; }
        public int GemReward { get; private set; }
        public string Icon { get; private set; } = string.Empty;
        public string BadgeColor { get; private set; } = string.Empty;
        public AchievementType Type { get; private set; }
        public Dictionary<string, object> Criteria { get; private set; } = new();
        public bool IsHidden { get; private set; }
        public int Order { get; private set; }

        private Achievement() { }

        public Achievement(
            string id,
            string name,
            string description,
            AchievementCategory category,
            AchievementRarity rarity,
            int requiredProgress,
            AchievementType type,
            string icon = "🏆",
            string badgeColor = "#FBBF24")
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Rarity = rarity;
            RequiredProgress = requiredProgress;
            Type = type;
            Icon = icon;
            BadgeColor = badgeColor;
            XpReward = CalculateXpReward(rarity);
            GemReward = CalculateGemReward(rarity);
        }

        public void SetHidden(bool value) => IsHidden = value;

        private static int CalculateXpReward(AchievementRarity rarity) => rarity switch
        {
            AchievementRarity.Common => 25,
            AchievementRarity.Uncommon => 50,
            AchievementRarity.Rare => 100,
            AchievementRarity.Epic => 250,
            AchievementRarity.Legendary => 500,
            AchievementRarity.Mythic => 1000,
            _ => 0
        };

        private static int CalculateGemReward(AchievementRarity rarity) => rarity switch
        {
            AchievementRarity.Common => 5,
            AchievementRarity.Uncommon => 10,
            AchievementRarity.Rare => 25,
            AchievementRarity.Epic => 50,
            AchievementRarity.Legendary => 100,
            AchievementRarity.Mythic => 250,
            _ => 0
        };
    }
}