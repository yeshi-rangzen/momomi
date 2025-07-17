using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGloballyDiscoverableField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_globally_discoverable",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_globally_discoverable",
                table: "users",
                column: "is_globally_discoverable",
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_users_globally_discoverable",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_globally_discoverable",
                table: "users");
        }
    }
}
