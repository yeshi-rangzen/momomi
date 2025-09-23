using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUserReportsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_reports_users_reported_id",
                table: "user_reports");

            migrationBuilder.DropForeignKey(
                name: "FK_user_reports_users_reporter_id",
                table: "user_reports");

            migrationBuilder.DropIndex(
                name: "idx_user_reports_reported",
                table: "user_reports");

            migrationBuilder.DropIndex(
                name: "idx_user_reports_reporter",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "reported_id",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "reporter_id",
                table: "user_reports");

            migrationBuilder.AddColumn<string>(
                name: "reported_email",
                table: "user_reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "reported_gender",
                table: "user_reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporter_email",
                table: "user_reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reported",
                table: "user_reports",
                column: "reported_email");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reporter",
                table: "user_reports",
                column: "reporter_email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_user_reports_reported",
                table: "user_reports");

            migrationBuilder.DropIndex(
                name: "idx_user_reports_reporter",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "reported_email",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "reported_gender",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "reporter_email",
                table: "user_reports");

            migrationBuilder.AddColumn<Guid>(
                name: "reported_id",
                table: "user_reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "reporter_id",
                table: "user_reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reported",
                table: "user_reports",
                column: "reported_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reporter",
                table: "user_reports",
                column: "reporter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_reports_users_reported_id",
                table: "user_reports",
                column: "reported_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_reports_users_reporter_id",
                table: "user_reports",
                column: "reporter_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
