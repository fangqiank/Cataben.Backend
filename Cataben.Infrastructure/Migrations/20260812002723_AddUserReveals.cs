using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cataben.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReveals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: 3 backfills the global reveal budget onto pre-existing user rows
            // (new users also start at 3 via the User.RevealsRemaining field initializer).
            migrationBuilder.AddColumn<int>(
                name: "RevealsRemaining",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevealsRemaining",
                table: "Users");
        }
    }
}
