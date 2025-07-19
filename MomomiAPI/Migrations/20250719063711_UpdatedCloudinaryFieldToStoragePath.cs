using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomomiAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedCloudinaryFieldToStoragePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "cloudinary_public_id",
                table: "user_photos",
                newName: "storage_path");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "storage_path",
                table: "user_photos",
                newName: "cloudinary_public_id");
        }
    }
}
