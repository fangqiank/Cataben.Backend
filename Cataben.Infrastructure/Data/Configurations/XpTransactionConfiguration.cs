using Cataben.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    /// <summary>
    /// EF mapping for <see cref="XpTransaction"/>. Picked up automatically by
    /// <c>ApplyConfigurationsFromAssembly</c>. Source 枚举按 int 存（同 User.Role / Quest.Cadence 约定）。
    /// </summary>
    public class XpTransactionConfiguration : IEntityTypeConfiguration<XpTransaction>
    {
        public void Configure(EntityTypeBuilder<XpTransaction> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount).IsRequired();
            builder.Property(x => x.Source).HasConversion<int>();
            builder.Property(x => x.SourceId).HasMaxLength(100);
            builder.Property(x => x.Reason).HasMaxLength(200);

            // 时段聚合主索引（WHERE CreatedAt>=since GROUP BY UserId）+ 按来源溯源。
            builder.HasIndex(x => new { x.UserId, x.CreatedAt });
            builder.HasIndex(x => new { x.Source, x.SourceId });
        }
    }
}
