namespace Cataben.Domain.Entities
{
    /// <summary>
    /// A user's ownership of a <see cref="Reward"/> — one row per (User, Reward); buy-once-own-forever.
    /// For Title/Theme categories, <see cref="IsEquipped"/> marks the single active item in that category.
    /// </summary>
    public class UserReward
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; } = null!;
        public Guid RewardId { get; private set; }
        public Reward Reward { get; private set; } = null!;
        public DateTime RedeemedAt { get; private set; }
        public bool IsEquipped { get; private set; }

        private UserReward() { }

        public UserReward(User user, Reward reward)
        {
            Id = Guid.NewGuid();
            UserId = user.Id;
            User = user;
            RewardId = reward.Id;
            Reward = reward;
            RedeemedAt = DateTime.UtcNow;
            IsEquipped = false;
        }

        public void Equip() => IsEquipped = true;

        public void Unequip() => IsEquipped = false;
    }
}
