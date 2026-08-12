using System.Text.Json;
using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    /// <summary>
    /// EF mapping for <see cref="UserQuest"/>. The unique index on
    /// (UserId, QuestId, WindowStart) is the heart of the lazy-window design: each new cadence window
    /// is a fresh row, so "reset" is free and per-window history is preserved — no background job needed.
    /// </summary>
    public class UserQuestConfiguration : IEntityTypeConfiguration<UserQuest>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<UserQuest> builder)
        {
            builder.HasKey(uq => uq.Id);

            // Hard reset / history key: one row per (User, Quest, Window).
            builder.HasIndex(uq => new { uq.UserId, uq.QuestId, uq.WindowStart }).IsUnique();
            builder.HasIndex(uq => new { uq.UserId, uq.WindowStart });

            builder.Property(uq => uq.QuestId).IsRequired().HasMaxLength(100);

            builder.Property(uq => uq.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, _jsonOptions) ?? new())
                .Metadata.SetValueComparer(JsonValueComparers.StringObjectDictionary);

            builder.HasOne(uq => uq.Quest)
                .WithMany()
                .HasForeignKey(uq => uq.QuestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(uq => uq.User)
                .WithMany()
                .HasForeignKey(uq => uq.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
