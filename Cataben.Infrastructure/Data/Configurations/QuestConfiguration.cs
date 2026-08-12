using System.Text.Json;
using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    /// <summary>
    /// EF mapping for <see cref="Quest"/>. Picked up automatically by
    /// <c>ApplyConfigurationsFromAssembly</c>, so no inline config is added in AppDbContext.
    /// Mirrors the inline <see cref="Achievement"/> mapping: Criteria dictionary persisted as JSONB.
    /// </summary>
    public class QuestConfiguration : IEntityTypeConfiguration<Quest>
    {
        // Same options as AppDbContext._jsonOptions / JsonValueComparers so serialize/deserialize stay consistent.
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<Quest> builder)
        {
            builder.HasKey(q => q.Id);

            builder.Property(q => q.Id).IsRequired().HasMaxLength(100);
            builder.Property(q => q.Name).IsRequired().HasMaxLength(100);
            builder.Property(q => q.Description).IsRequired().HasMaxLength(500);
            builder.Property(q => q.Icon).HasMaxLength(20);

            // Enums persisted as int (matches User.Role convention).
            builder.Property(q => q.Cadence).HasConversion<int>();
            builder.Property(q => q.Metric).HasConversion<int>();

            builder.Property(q => q.Criteria)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, _jsonOptions) ?? new())
                .Metadata.SetValueComparer(JsonValueComparers.StringObjectDictionary);

            builder.HasIndex(q => q.Cadence);
            builder.HasIndex(q => q.IsActive);
        }
    }
}
