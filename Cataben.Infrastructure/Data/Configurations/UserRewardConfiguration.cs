using Cataben.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    /// <summary>
    /// EF mapping for <see cref="UserReward"/>. The unique index on (UserId, RewardId) enforces
    /// buy-once-own-forever semantics and guards against concurrent duplicate redemptions.
    /// </summary>
    public class UserRewardConfiguration : IEntityTypeConfiguration<UserReward>
    {
        public void Configure(EntityTypeBuilder<UserReward> builder)
        {
            builder.HasKey(ur => ur.Id);

            // Buy-once-own-forever: one row per (User, Reward).
            builder.HasIndex(ur => new { ur.UserId, ur.RewardId }).IsUnique();
            builder.HasIndex(ur => ur.RewardId);

            builder.HasOne(ur => ur.Reward)
                .WithMany()
                .HasForeignKey(ur => ur.RewardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ur => ur.User)
                .WithMany()
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
