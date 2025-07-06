using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDisoverableToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_discoverable",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notifications_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "push_token",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "user_reports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "admin_notes",
                table: "user_reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                table: "user_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "like_type",
                table: "user_likes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "push_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notification_type = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "text", nullable: true),
                    is_sent = table.Column<bool>(type: "boolean", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_push_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_blocked_user_id",
                        column: x => x.blocked_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_blocker_user_id",
                        column: x => x.blocker_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_type = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_usage_limits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    likes_used_today = table.Column<int>(type: "integer", nullable: false),
                    super_likes_used_today = table.Column<int>(type: "integer", nullable: false),
                    super_likes_used_this_week = table.Column<int>(type: "integer", nullable: false),
                    ads_watched_today = table.Column<int>(type: "integer", nullable: false),
                    bonus_likes_from_ads = table.Column<int>(type: "integer", nullable: false),
                    last_reset_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_weekly_reset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_usage_limits", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_usage_limits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_users_discoverable",
                table: "users",
                column: "is_discoverable",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reason",
                table: "user_reports",
                column: "reason");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_type_created",
                table: "user_likes",
                columns: new[] { "like_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_push_notifications_sent_created",
                table: "push_notifications",
                columns: new[] { "is_sent", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_push_notifications_user_id",
                table: "push_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_push_notifications_user_read",
                table: "push_notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "idx_user_blocks_blocked",
                table: "user_blocks",
                column: "blocked_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_blocks_blocker",
                table: "user_blocks",
                column: "blocker_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_blocks_unique",
                table: "user_blocks",
                columns: new[] { "blocker_user_id", "blocked_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_active_expires",
                table: "user_subscriptions",
                columns: new[] { "is_active", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_user_id",
                table: "user_subscriptions",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_usage_limits_reset_date",
                table: "user_usage_limits",
                column: "last_reset_date");

            migrationBuilder.CreateIndex(
                name: "idx_user_usage_limits_user_id",
                table: "user_usage_limits",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_notifications");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "user_usage_limits");

            migrationBuilder.DropIndex(
                name: "idx_users_discoverable",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_user_reports_reason",
                table: "user_reports");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_type_created",
                table: "user_likes");

            migrationBuilder.DropColumn(
                name: "is_discoverable",
                table: "users");

            migrationBuilder.DropColumn(
                name: "notifications_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "push_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "admin_notes",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "user_reports");

            migrationBuilder.DropColumn(
                name: "like_type",
                table: "user_likes");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "user_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
