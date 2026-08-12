using System.Text.Json;
using Cataben.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options): DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Challenge> Challenges { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Achievement> Achievements { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<LearningPath> LearningPaths { get; set; }
        public DbSet<UserLearningPath> UserLearningPaths { get; set; }
        public DbSet<Quest> Quests { get; set; }
        public DbSet<UserQuest> UserQuests { get; set; }
        public DbSet<XpTransaction> XpTransactions { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<UserReward> UserRewards { get; set; }

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all IEntityTypeConfiguration<T> classes in this assembly
            // (UserConfiguration, ChallengeConfiguration, SubmissionConfiguration).
            // These were previously defined but never applied, which left several
            // value objects / Dictionary properties unmapped and crashed at runtime.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Entities without a dedicated configuration class are configured inline.
            // Dictionary<string,object> properties are persisted as JSONB via value converters.
            modelBuilder.Entity<Achievement>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.HasIndex(a => a.Category);

                entity.Property(a => a.Name).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Description).IsRequired().HasMaxLength(500);
                entity.Property(a => a.Criteria)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, _jsonOptions),
                        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, _jsonOptions) ?? new())
                    .Metadata.SetValueComparer(JsonValueComparers.StringObjectDictionary);
            });

            modelBuilder.Entity<UserAchievement>(entity =>
            {
                entity.HasKey(ua => ua.Id);
                entity.HasIndex(ua => new { ua.UserId, ua.AchievementId }).IsUnique();
                entity.Property(ua => ua.Metadata)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, _jsonOptions),
                        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, _jsonOptions) ?? new())
                    .Metadata.SetValueComparer(JsonValueComparers.StringObjectDictionary);
            });

            modelBuilder.Entity<LearningPath>(entity =>
            {
                entity.HasKey(lp => lp.Id);
                entity.Property(lp => lp.Name).IsRequired().HasMaxLength(200);
                entity.Property(lp => lp.Description).IsRequired().HasMaxLength(1000);
            });

            modelBuilder.Entity<UserLearningPath>(entity =>
            {
                entity.HasKey(ulp => ulp.Id);
                entity.HasIndex(ulp => new { ulp.UserId, ulp.LearningPathId }).IsUnique();
            });
        }
    }
}
