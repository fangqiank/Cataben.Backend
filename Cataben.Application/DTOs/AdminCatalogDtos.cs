using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs;

// Create payloads for the admin catalog endpoints (Quest/Achievement/Reward). These three are
// immutable templates once created, so admin only supports create + a single boolean toggle (active/
// hidden). Fields map 1:1 to each entity's constructor. Achievement omits Xp/Gem — those are derived
// from Rarity inside the constructor and surfaced read-only on the list view.

public class QuestCreateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestCadence Cadence { get; set; }
    public QuestMetric Metric { get; set; }
    public int Threshold { get; set; }
    public int XpReward { get; set; }
    public int GemReward { get; set; }
    public string Icon { get; set; } = "🎯";
    public int Order { get; set; }
    public Dictionary<string, object> Criteria { get; set; } = new();
}

public class AchievementCreateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AchievementCategory Category { get; set; }
    public AchievementRarity Rarity { get; set; }
    public int RequiredProgress { get; set; }
    public AchievementType Type { get; set; }
    public string Icon { get; set; } = "🏆";
    public string BadgeColor { get; set; } = "#FBBF24";
    public bool IsHidden { get; set; }
    public int Order { get; set; }
    public Dictionary<string, object> Criteria { get; set; } = new();
}

public class RewardCreateDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RewardCategory Category { get; set; }
    public int Cost { get; set; }
    public bool IsProOnly { get; set; }
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsDefault { get; set; }
}

// Admin catalog list/response shapes (catalog fields only — no per-user state).

public class AdminQuestDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestCadence Cadence { get; set; }
    public QuestMetric Metric { get; set; }
    public int Threshold { get; set; }
    public int XpReward { get; set; }
    public int GemReward { get; set; }
    public string Icon { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Order { get; set; }
}

public class AdminAchievementDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AchievementCategory Category { get; set; }
    public AchievementRarity Rarity { get; set; }
    public int RequiredProgress { get; set; }
    public int XpReward { get; set; }
    public int GemReward { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = string.Empty;
    public AchievementType Type { get; set; }
    public bool IsHidden { get; set; }
    public int Order { get; set; }
}

public class AdminRewardDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RewardCategory Category { get; set; }
    public int Cost { get; set; }
    public bool IsProOnly { get; set; }
    public bool IsDefault { get; set; }
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsActive { get; set; }
}

