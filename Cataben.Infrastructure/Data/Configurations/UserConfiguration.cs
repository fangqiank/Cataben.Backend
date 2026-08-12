using Cataben.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.HasIndex(u => u.Email).IsUnique();
            builder.HasIndex(u => u.ExternalId).IsUnique();

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.ExternalId)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(u => u.Role)
                .HasConversion<int>();

            builder.Property(u => u.PreferredTheme)
                .HasMaxLength(50);

            builder.Property(u => u.AvatarUrl)
                .HasMaxLength(500);

            // Relationships
            builder.HasMany(u => u.UserAchievements)
                .WithOne(ua => ua.User)
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.Submissions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
