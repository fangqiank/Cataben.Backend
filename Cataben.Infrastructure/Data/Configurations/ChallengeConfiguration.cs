using System.Text.Json;
using Cataben.Domain.Entities;
using Cataben.Domain.ValueObjects;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    public class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
    {
        // Same options as AppDbContext._jsonOptions / JsonValueComparers so serialize/deserialize stay consistent.
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<Challenge> builder)
        {
            builder.HasKey(c => c.Id);

            builder.HasIndex(c => c.Title);
            builder.HasIndex(c => c.Category);
            builder.HasIndex(c => c.Type);
            builder.HasIndex(c => c.LearningPathId);

            builder.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.Description)
                .IsRequired();

            builder.Property(c => c.Category)
                .HasMaxLength(100);

            builder.Property(c => c.Difficulty)
                .HasConversion(
                    d => d.Name,
                    d => Difficulty.FromName(d)
                )
                .HasMaxLength(20);

            builder.Property(c => c.InitialCode)
                .IsRequired();

            builder.Property(c => c.SolutionCode)
                .IsRequired();

            // Hints persisted as a JSON array (List<string> -> text/jsonb via converter + comparer).
            builder.Property(c => c.Hints)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    // Tolerate pre-existing rows backfilled with "" by the migration (empty string is not valid JSON).
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, _jsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(JsonValueComparers.StringList);

            // Owns test cases as JSON
            builder.OwnsMany(c => c.TestCases, tc =>
            {
                tc.Property(t => t.Id).IsRequired();
                tc.Property(t => t.Name).IsRequired().HasMaxLength(100);
                tc.Property(t => t.Input).IsRequired();
                tc.Property(t => t.ExpectedOutput).IsRequired();
                tc.Property(t => t.IsPublic).IsRequired();
                tc.Property(t => t.Weight).IsRequired();
            });

            // Owns hidden tests as JSON
            builder.OwnsMany(c => c.HiddenTests, ht =>
            {
                ht.Property(h => h.Id).IsRequired();
                ht.Property(h => h.Name).IsRequired().HasMaxLength(100);
                ht.Property(h => h.Input).IsRequired();
                ht.Property(h => h.ExpectedOutput).IsRequired();
                ht.Property(h => h.ValidationType).HasMaxLength(50);
                ht.Property(h => h.MinScore).IsRequired();
            });
        }
    }
}
