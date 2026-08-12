namespace Cataben.Domain.Events
{
    public class UserLevelUpEvent(Guid userId, int oldLevel, int newLevel, int xpEarned)
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; } = userId;
        public int OldLevel { get; private set; } = oldLevel;
        public int NewLevel { get; private set; } = newLevel;
        public int XpEarned { get; private set; } = xpEarned;
        public DateTime LeveledUpAt { get; private set; } = DateTime.UtcNow;
    }
}
