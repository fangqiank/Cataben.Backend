using System.Text.Json;
using Cataben.Domain.Entities;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cataben.Infrastructure.Data.Configurations
{
    public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => s.UserId);
            builder.HasIndex(s => s.ChallengeId);
            builder.HasIndex(s => s.Status);
            builder.HasIndex(s => new { s.UserId, s.ChallengeId });
            builder.HasIndex(s => new { s.UserId, s.ChallengeId })
                .HasDatabaseName("IX_Submissions_UserId_ChallengeId_Successful")
                .IsUnique()
                .HasFilter("\"IsSuccessful\" = true AND \"Status\" = 4");
            builder.HasIndex(s => s.SubmittedAt);

            builder.Property(s => s.Code)
                .IsRequired();

            builder.Property(s => s.Status)
                .HasConversion<int>();

            builder.Property(s => s.ErrorMessage)
                .HasMaxLength(1000);

            builder.Property(s => s.UserAgent)
                .HasMaxLength(500);

            builder.Property(s => s.IpAddress)
                .HasMaxLength(45);

            // Metadata is a Dictionary<string,object> persisted as JSONB.
            builder.Property(s => s.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, _jsonOptions) ?? new())
                .Metadata.SetValueComparer(JsonValueComparers.StringObjectDictionary);

            // Owns status history as JSON
            builder.OwnsMany(s => s.StatusHistory, sh =>
            {
                sh.Property(h => h.Status).IsRequired();
                sh.Property(h => h.Timestamp).IsRequired();
                sh.Property(h => h.Reason).HasMaxLength(500);
            });

            // Owns test results as JSON
            builder.OwnsMany(s => s.TestResults, tr =>
            {
                tr.Property(t => t.Id).IsRequired();
                tr.Property(t => t.Name).IsRequired().HasMaxLength(100);
                tr.Property(t => t.Passed).IsRequired();
                tr.Property(t => t.Score).IsRequired();
                tr.Property(t => t.Message).HasMaxLength(500);
            });

            // Relationships
            builder.HasOne(s => s.User)
                .WithMany(u => u.Submissions)
                .HasForeignKey(s => s.UserId);

            builder.HasOne(s => s.Challenge)
                .WithMany()
                .HasForeignKey(s => s.ChallengeId);
        }
    }
}
