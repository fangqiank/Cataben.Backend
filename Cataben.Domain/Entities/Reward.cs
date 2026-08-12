using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    /// <summary>
    /// A redeemable shop item (catalog definition, like Achievement/Quest). Users spend gems to unlock
    /// rewards (titles, themes, streak-freeze packs). <see cref="IsProOnly"/> rewards can never be bought
    /// with gems (membership-gated). The single <see cref="IsDefault"/> theme is virtually owned and
    /// equipped by every user until they equip something else.
    /// </summary>
    public class Reward
    {
        public Guid Id { get; private set; }
        public string Key { get; private set; } = string.Empty;     // stable semantic id, e.g. "bug-hunter"
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public RewardCategory Category { get; private set; }
        public int Cost { get; private set; }                       // gem cost; 0 = free (e.g. default theme)
        public bool IsProOnly { get; private set; }
        public bool IsDefault { get; private set; }
        public string Icon { get; private set; } = string.Empty;    // lucide icon key, e.g. "bug"
        public int Order { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private Reward() { }

        public Reward(
            string key,
            string name,
            string description,
            RewardCategory category,
            int cost,
            bool isProOnly,
            string icon,
            int order,
            bool isDefault = false)
        {
            Id = Guid.NewGuid();
            Key = key;
            Name = name;
            Description = description;
            Category = category;
            Cost = cost;
            IsProOnly = isProOnly;
            IsDefault = isDefault;
            Icon = icon;
            Order = order;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public void SetActive(bool value) => IsActive = value;
    }
}
