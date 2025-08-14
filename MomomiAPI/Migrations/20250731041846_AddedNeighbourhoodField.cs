using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedNeighbourhoodField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropIndex(
                name: "idx_users_active",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_age",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_discoverable",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_global_discovery",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_globally_discoverable",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_heritage",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_location",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_user_photos_primary",
                table: "user_photos");

            migrationBuilder.DropIndex(
                name: "idx_user_photos_user_id",
                table: "user_photos");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_liked",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_match",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_type_created",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_messages_conversation",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_sender_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_conversations_user2_id",
                table: "conversations");

            migrationBuilder.RenameIndex(
                name: "idx_user_likes_liker",
                table: "user_likes",
                newName: "idx_user_likes_liker_discovery");

            migrationBuilder.RenameIndex(
                name: "idx_messages_unread",
                table: "messages",
                newName: "idx_messages_conversation_read");

            migrationBuilder.RenameIndex(
                name: "idx_conversations_users",
                table: "conversations",
                newName: "idx_conversations_users_unique");

            migrationBuilder.AlterColumn<bool>(
                name: "is_globally_discoverable",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "enable_global_discovery",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "neighbourhood",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_active_last",
                table: "users",
                columns: new[] { "is_active", "last_active" },
                descending: new[] { false, true },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_age_gender",
                table: "users",
                columns: new[] { "is_active", "date_of_birth", "gender" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_discovery_main",
                table: "users",
                columns: new[] { "is_active", "is_discoverable", "gender", "interested_in" },
                filter: "is_active = true AND is_discoverable = true")
                .Annotation("Npgsql:IndexInclude", new[] { "date_of_birth", "heritage", "latitude", "longitude", "enable_global_discovery", "is_globally_discoverable", "last_active" });

            migrationBuilder.CreateIndex(
                name: "idx_users_global_discovery",
                table: "users",
                columns: new[] { "is_active", "is_globally_discoverable", "enable_global_discovery", "gender" },
                filter: "is_active = true AND is_globally_discoverable = true")
                .Annotation("Npgsql:IndexInclude", new[] { "date_of_birth", "heritage", "interested_in", "last_active" });

            migrationBuilder.CreateIndex(
                name: "idx_users_location_discovery",
                table: "users",
                columns: new[] { "is_active", "is_discoverable", "latitude", "longitude" },
                filter: "is_active = true AND is_discoverable = true AND latitude IS NOT NULL AND longitude IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_user_photos_display",
                table: "user_photos",
                columns: new[] { "user_id", "is_primary", "photo_order" })
                .Annotation("Npgsql:IndexInclude", new[] { "url", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_user_photos_order",
                table: "user_photos",
                columns: new[] { "user_id", "photo_order" })
                .Annotation("Npgsql:IndexInclude", new[] { "is_primary", "url" });

            migrationBuilder.CreateIndex(
                name: "idx_user_photos_primary",
                table: "user_photos",
                column: "user_id",
                filter: "is_primary = true")
                .Annotation("Npgsql:IndexInclude", new[] { "url" });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_composite_main",
                table: "user_likes",
                columns: new[] { "liker_user_id", "liked_user_id", "is_match", "is_like" });

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
                name: "index_user_likes_liked_matches",
                table: "user_likes",
                columns: new[] { "liked_user_id", "is_match", "is_like" },
                filter: "is_match = true AND is_like = true");

            migrationBuilder.CreateIndex(
                name: "idx_messages_last_message",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at" },
                descending: new[] { false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "sender_id", "content", "message_type", "is_read" });

            migrationBuilder.CreateIndex(
                name: "idx_messages_sender_time",
                table: "messages",
                columns: new[] { "sender_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_messages_unread_count",
                table: "messages",
                columns: new[] { "conversation_id", "sender_id", "is_read" },
                filter: "is_read = false")
                .Annotation("Npgsql:IndexInclude", new[] { "sent_at" });

            migrationBuilder.CreateIndex(
                name: "idx_conversations_user1_active",
                table: "conversations",
                columns: new[] { "user1_id", "is_active", "updated_at" },
                descending: new[] { false, false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "user2_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_conversations_user2_active",
                table: "conversations",
                columns: new[] { "user2_id", "is_active", "updated_at" },
                descending: new[] { false, false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "user1_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_conversations_users_active",
                table: "conversations",
                columns: new[] { "user1_id", "user2_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_users_active_last",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_age_gender",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_discovery_main",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_global_discovery",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_users_location_discovery",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_user_photos_display",
                table: "user_photos");

            migrationBuilder.DropIndex(
                name: "idx_user_photos_order",
                table: "user_photos");

            migrationBuilder.DropIndex(
                name: "idx_user_photos_primary",
                table: "user_photos");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_composite_main",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_liker_recent",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_user_likes_received",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "index_user_likes_liked_matches",
                table: "user_likes");

            migrationBuilder.DropIndex(
                name: "idx_messages_last_message",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "idx_messages_sender_time",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "idx_messages_unread_count",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "idx_conversations_user1_active",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "idx_conversations_user2_active",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "idx_conversations_users_active",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "neighbourhood",
                table: "users");

            migrationBuilder.RenameIndex(
                name: "idx_user_likes_liker_discovery",
                table: "user_likes",
                newName: "idx_user_likes_liker");

            migrationBuilder.RenameIndex(
                name: "idx_messages_conversation_read",
                table: "messages",
                newName: "idx_messages_unread");

            migrationBuilder.RenameIndex(
                name: "idx_conversations_users_unique",
                table: "conversations",
                newName: "idx_conversations_users");

            migrationBuilder.AlterColumn<bool>(
                name: "is_globally_discoverable",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "enable_global_discovery",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "idx_users_active",
                table: "users",
                columns: new[] { "is_active", "last_active" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_age",
                table: "users",
                column: "date_of_birth",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_discoverable",
                table: "users",
                column: "is_discoverable",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_global_discovery",
                table: "users",
                column: "enable_global_discovery",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_globally_discoverable",
                table: "users",
                column: "is_globally_discoverable",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_heritage",
                table: "users",
                column: "heritage",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_users_location",
                table: "users",
                columns: new[] { "latitude", "longitude" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_user_photos_primary",
                table: "user_photos",
                column: "user_id",
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "idx_user_photos_user_id",
                table: "user_photos",
                columns: new[] { "user_id", "photo_order" });

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_liked",
                table: "user_likes",
                column: "liked_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_match",
                table: "user_likes",
                column: "is_match",
                filter: "is_match = true");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_type_created",
                table: "user_likes",
                columns: new[] { "like_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_messages_conversation",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_user2_id",
                table: "conversations",
                column: "user2_id");

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
        }
    }
}
