using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supabase_uid = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: true),
                    interested_in = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    heritage = table.Column<string>(type: "text", nullable: true),
                    religion = table.Column<string>(type: "text", nullable: false),
                    languages_spoken = table.Column<List<string>>(type: "text[]", nullable: true),
                    education_level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    height_cm = table.Column<int>(type: "integer", nullable: true),
                    max_distance_km = table.Column<int>(type: "integer", nullable: false),
                    min_age = table.Column<int>(type: "integer", nullable: false),
                    max_age = table.Column<int>(type: "integer", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_active = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user1_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user2_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.CheckConstraint("CHK_Conversation_UserOrder", "user1_id < user2_id");
                    table.ForeignKey(
                        name: "FK_conversations_users_user1_id",
                        column: x => x.user1_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversations_users_user2_id",
                        column: x => x.user2_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    liker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_like = table.Column<bool>(type: "boolean", nullable: false),
                    is_match = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
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

            migrationBuilder.CreateTable(
                name: "user_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cloudinary_public_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    photo_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_photos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_heritage = table.Column<string>(type: "text", nullable: true),
                    preferred_religions = table.Column<string>(type: "text", nullable: true),
                    cultural_importance_level = table.Column<int>(type: "integer", nullable: false),
                    language_preference = table.Column<List<string>>(type: "text[]", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_reports_users_reported_id",
                        column: x => x.reported_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_conversations_users",
                table: "conversations",
                columns: new[] { "user1_id", "user2_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_user2_id",
                table: "conversations",
                column: "user2_id");

            migrationBuilder.CreateIndex(
                name: "idx_messages_conversation",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_messages_unread",
                table: "messages",
                columns: new[] { "conversation_id", "is_read" },
                filter: "is_read = false");

            migrationBuilder.CreateIndex(
                name: "IX_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_liked",
                table: "user_likes",
                column: "liked_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_liker",
                table: "user_likes",
                column: "liker_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_match",
                table: "user_likes",
                column: "is_match",
                filter: "is_match = true");

            migrationBuilder.CreateIndex(
                name: "idx_user_likes_unique",
                table: "user_likes",
                columns: new[] { "liker_user_id", "liked_user_id" },
                unique: true);

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
                name: "idx_user_preferences_user_id",
                table: "user_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reported",
                table: "user_reports",
                column: "reported_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_reporter",
                table: "user_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_reports_status",
                table: "user_reports",
                column: "status");

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
                name: "idx_users_supabase_uid",
                table: "users",
                column: "supabase_uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "user_likes");

            migrationBuilder.DropTable(
                name: "user_photos");

            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropTable(
                name: "user_reports");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
