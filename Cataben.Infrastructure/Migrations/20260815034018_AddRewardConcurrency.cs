using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cataben.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId_ChallengeId",
                table: "Submissions");

            // Pre-clean before the partial unique index below: drop all but the EARLIEST
            // successful submission per (UserId, ChallengeId). Older data can contain several
            // (the "already solved" guard is recent), and any surviving duplicate would make
            // CREATE UNIQUE INDEX fail — which, with startup auto-migration, crash-loops the
            // API. Tie-break on Id keeps the deletion deterministic. TestResults/StatusHistory
            // go with the rows (ON DELETE CASCADE).
            migrationBuilder.Sql(@"
                DELETE FROM ""Submissions"" s
                USING ""Submissions"" earlier
                WHERE earlier.""UserId"" = s.""UserId""
                  AND earlier.""ChallengeId"" = s.""ChallengeId""
                  AND earlier.""IsSuccessful"" = TRUE AND earlier.""Status"" = 4
                  AND s.""IsSuccessful"" = TRUE AND s.""Status"" = 4
                  AND (earlier.""SubmittedAt"", earlier.""Id"") < (s.""SubmittedAt"", s.""Id"")");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_ChallengeId_Successful",
                table: "Submissions",
                columns: new[] { "UserId", "ChallengeId" },
                unique: true,
                filter: "\"IsSuccessful\" = true AND \"Status\" = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId_ChallengeId_Successful",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_ChallengeId",
                table: "Submissions",
                columns: new[] { "UserId", "ChallengeId" });
        }
    }
}
