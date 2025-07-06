using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserLikeConfiguration : IEntityTypeConfiguration<UserLike>
    {
        public void Configure(EntityTypeBuilder<UserLike> builder)
        {
            builder.HasKey(ul => ul.Id);

            builder.HasIndex(ul => new { ul.LikerUserId, ul.LikedUserId })
                .IsUnique()
                .HasDatabaseName("idx_user_likes_unique");

            builder.HasIndex(ul => ul.LikerUserId)
                .HasDatabaseName("idx_user_likes_liker");

            builder.HasIndex(ul => ul.LikedUserId)
                .HasDatabaseName("idx_user_likes_liked");

            builder.HasIndex(ul => ul.IsMatch)
                .HasDatabaseName("idx_user_likes_match")
                .HasFilter("is_match = true");

            builder.HasIndex(ul => new { ul.LikeType, ul.CreatedAt })
                .HasDatabaseName("idx_user_likes_type_created");

            builder.Property(ul => ul.LikeType)
                .HasConversion<string>();

            // Configure explicit relationships to avoid ambiguity
            builder.HasOne(ul => ul.LikerUser)
                .WithMany(u => u.LikesGiven)
                .HasForeignKey(ul => ul.LikerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ul => ul.LikedUser)
                .WithMany(u => u.LikesReceived)
                .HasForeignKey(ul => ul.LikedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
