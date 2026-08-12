using Cataben.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    /// <summary>
    /// EF mapping for <see cref="Reward"/>. Picked up automatically by
    /// <c>ApplyConfigurationsFromAssembly</c>. Enums persisted as int (matches User.Role convention).
    /// </summary>
    public class RewardConfiguration : IEntityTypeConfiguration<Reward>
    {
        public void Configure(EntityTypeBuilder<Reward> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Key).IsRequired().HasMaxLength(100);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.Property(r => r.Description).IsRequired().HasMaxLength(500);
            builder.Property(r => r.Icon).HasMaxLength(40);

            builder.Property(r => r.Category).HasConversion<int>();

            builder.HasIndex(r => r.Key).IsUnique();
            builder.HasIndex(r => r.Category);
            builder.HasIndex(r => r.IsActive);
        }
    }
}
