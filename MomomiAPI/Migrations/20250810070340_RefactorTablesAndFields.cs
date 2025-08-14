using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTablesAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_user_subscriptions_active_expires",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "bonus_likes_from_ads",
                table: "user_usage_limits");

            migrationBuilder.DropColumn(
                name: "last_weekly_reset",
                table: "user_usage_limits");

            migrationBuilder.DropColumn(
                name: "super_likes_used_this_week",
                table: "user_usage_limits");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "user_subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "religion",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "longitude",
                table: "users",
                type: "numeric(11,8)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(11,8)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "latitude",
                table: "users",
                type: "numeric(10,8)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,8)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "languages_spoken",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "interested_in",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hometown",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "heritage",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "height_cm",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_of_birth",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "user_swipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    swiper_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    swiped_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    swipe_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_swipes", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_swipes_users_swiped_user_id",
                        column: x => x.swiped_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_swipes_users_swiper_user_id",
                        column: x => x.swiper_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_active_expires",
                table: "user_subscriptions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_user_swipes_received",
                table: "user_swipes",
                columns: new[] { "swiper_user_id", "swipe_type" })
                .Annotation("Npgsql:IndexInclude", new[] { "swiped_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_user_swipes_recent",
                table: "user_swipes",
                columns: new[] { "swiper_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_user_swipes_unique",
                table: "user_swipes",
                columns: new[] { "swiper_user_id", "swiped_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_swipes_swiped_user_id",
                table: "user_swipes",
                column: "swiped_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_swipes");

            migrationBuilder.DropIndex(
                name: "idx_user_subscriptions_active_expires",
                table: "user_subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "religion",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "longitude",
                table: "users",
                type: "numeric(11,8)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(11,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "latitude",
                table: "users",
                type: "numeric(10,8)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,8)");

            migrationBuilder.AlterColumn<string>(
                name: "languages_spoken",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "interested_in",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "hometown",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "heritage",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "height_cm",
                table: "users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_of_birth",
                table: "users",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "bonus_likes_from_ads",
                table: "user_usage_limits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_weekly_reset",
                table: "user_usage_limits",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "super_likes_used_this_week",
                table: "user_usage_limits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "user_subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "user_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    liked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_like = table.Column<bool>(type: "boolean", nullable: false),
                    is_match = table.Column<bool>(type: "boolean", nullable: false),
                    like_type = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_likes", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_likes_users_liked_user_id",
                        column: x => x.liked_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_likes_users_liker_user_id",
                        column: x => x.liker_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_active_expires",
                table: "user_subscriptions",
                columns: new[] { "is_active", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_composite_main",
                table: "user_likes",
                columns: new[] { "liker_user_id", "liked_user_id", "is_match", "is_like" });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_liker_discovery",
                table: "user_likes",
                column: "liker_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_liker_recent",
                table: "user_likes",
                columns: new[] { "liker_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_received",
                table: "user_likes",
                columns: new[] { "liked_user_id", "is_like", "is_match" },
                filter: "is_like = true")
                .Annotation("Npgsql:IndexInclude", new[] { "liker_user_id", "created_at", "like_type" });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_unique",
                table: "user_likes",
                columns: new[] { "liker_user_id", "liked_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "index_user_likes_liked_matches",
                table: "user_likes",
                columns: new[] { "liked_user_id", "is_match", "is_like" },
                filter: "is_match = true AND is_like = true");
        }
    }
}
