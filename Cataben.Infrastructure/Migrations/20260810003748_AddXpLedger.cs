using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cataben.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddXpLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XpTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XpTransactions_Source_SourceId",
                table: "XpTransactions",
                columns: new[] { "Source", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_XpTransactions_UserId_CreatedAt",
                table: "XpTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            // 回填历史 XP 流水（一次性，从现有 3 张表精确重建，让时段榜从启用之日起即有完整数据）：
            // 0=Challenge：成功提交各贡献 Challenge.XpReward。IsSuccessful=TRUE 即 Completed 且 ≥80% 通过。
            migrationBuilder.Sql(@"
                INSERT INTO ""XpTransactions"" (""Id"", ""UserId"", ""Amount"", ""Source"", ""SourceId"", ""Reason"", ""CreatedAt"")
                SELECT gen_random_uuid(), s.""UserId"", c.""XpReward"", 0, c.""Id""::text, NULL, s.""SubmittedAt""
                FROM ""Submissions"" s
                JOIN ""Challenges"" c ON c.""Id"" = s.""ChallengeId""
                WHERE s.""IsSuccessful"" = TRUE;
            ");

            // 1=Achievement：已解锁成就各贡献 Achievement.XpReward，时间取 CompletedAt（兜底 UnlockedAt）。
            migrationBuilder.Sql(@"
                INSERT INTO ""XpTransactions"" (""Id"", ""UserId"", ""Amount"", ""Source"", ""SourceId"", ""Reason"", ""CreatedAt"")
                SELECT gen_random_uuid(), ua.""UserId"", a.""XpReward"", 1, a.""Id"", NULL, COALESCE(ua.""CompletedAt"", ua.""UnlockedAt"")
                FROM ""UserAchievements"" ua
                JOIN ""Achievements"" a ON a.""Id"" = ua.""AchievementId""
                WHERE ua.""IsCompleted"" = TRUE AND a.""XpReward"" > 0;
            ");

            // 2=Quest：已领取任务各贡献 Quest.XpReward，时间取 ClaimedAt。
            migrationBuilder.Sql(@"
                INSERT INTO ""XpTransactions"" (""Id"", ""UserId"", ""Amount"", ""Source"", ""SourceId"", ""Reason"", ""CreatedAt"")
                SELECT gen_random_uuid(), uq.""UserId"", q.""XpReward"", 2, q.""Id"", NULL, uq.""ClaimedAt""
                FROM ""UserQuests"" uq
                JOIN ""Quests"" q ON q.""Id"" = uq.""QuestId""
                WHERE uq.""IsClaimed"" = TRUE AND q.""XpReward"" > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XpTransactions");
        }
    }
}
