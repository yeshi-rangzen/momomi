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

            // For loading user photos efficiently (includes all needed data)
            builder.HasIndex(up => new { up.UserId, up.IsPrimary, up.PhotoOrder })
                .HasDatabaseName("idx_user_photos_display")
                .IncludeProperties(up => new { up.Url, up.CreatedAt });

            // For getting primary photo quickly (most frequent query)
            builder.HasIndex(up => up.UserId)
                .HasDatabaseName("idx_user_photos_primary")
                .HasFilter("is_primary = true")
                .IncludeProperties(up => up.Url);

            // For photo ordering and management
            builder.HasIndex(up => new { up.UserId, up.PhotoOrder })
                .HasDatabaseName("idx_user_photos_order")
                .IncludeProperties(up => new { up.IsPrimary, up.Url });
        }
    }
}
