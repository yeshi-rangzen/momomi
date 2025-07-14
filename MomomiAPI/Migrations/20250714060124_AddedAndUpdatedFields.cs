using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedAndUpdatedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cultural_importance_level",
                table: "user_preferences");

            migrationBuilder.AlterColumn<string>(
                name: "languages_spoken",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "children",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "drinking",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "drugs",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family_plan",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hometown",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marijuana",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smoking",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "language_preference",
                table: "user_preferences",
                type: "text",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_children",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_drinking",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_drugs",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_education_levels",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_family_plans",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preferred_height_max",
                table: "user_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preferred_height_min",
                table: "user_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_marijuana",
                table: "user_preferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_smoking",
                table: "user_preferences",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "children",
                table: "users");

            migrationBuilder.DropColumn(
                name: "drinking",
                table: "users");

            migrationBuilder.DropColumn(
                name: "drugs",
                table: "users");

            migrationBuilder.DropColumn(
                name: "family_plan",
                table: "users");

            migrationBuilder.DropColumn(
                name: "hometown",
                table: "users");

            migrationBuilder.DropColumn(
                name: "marijuana",
                table: "users");

            migrationBuilder.DropColumn(
                name: "smoking",
                table: "users");

            migrationBuilder.DropColumn(
                name: "preferred_children",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_drinking",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_drugs",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_education_levels",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_family_plans",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_height_max",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_height_min",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_marijuana",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "preferred_smoking",
                table: "user_preferences");

            migrationBuilder.AlterColumn<List<string>>(
                name: "languages_spoken",
                table: "users",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "language_preference",
                table: "user_preferences",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cultural_importance_level",
                table: "user_preferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
