using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserPhotoConfiguration : IEntityTypeConfiguration<UserPhoto>
    {
        public void Configure(EntityTypeBuilder<UserPhoto> builder)
        {
            builder.HasKey(up => up.Id);

            builder.HasIndex(up => new { up.UserId, up.PhotoOrder })
                .HasDatabaseName("idx_user_photos_user_id");

            builder.HasIndex(up => up.UserId)
                .HasDatabaseName("idx_user_photos_primary")
                .HasFilter("is_primary = true");
        }
    }
}
