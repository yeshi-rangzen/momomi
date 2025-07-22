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

            // Composite index for match queries
            builder.HasIndex(ul => new { ul.LikerUserId, ul.LikedUserId, ul.IsMatch, ul.IsLike })
                .HasDatabaseName("idx_user_likes_composite_main");

            // For finding matches efficiently
            builder.HasIndex(ul => new { ul.LikedUserId, ul.IsMatch, ul.IsLike })
                .HasDatabaseName("index_user_likes_liked_matches")
                .HasFilter("is_match = true AND is_like = true");

            // For "users who like me" queries
            builder.HasIndex(ul => new { ul.LikedUserId, ul.IsLike, ul.IsMatch })
                .HasDatabaseName("idx_user_likes_received")
                .HasFilter("is_like = true")
                .IncludeProperties(ul => new { ul.LikerUserId, ul.CreatedAt, ul.LikeType });

            // For undo last swipe functionality
            builder.HasIndex(ul => new { ul.LikerUserId, ul.CreatedAt })
                .HasDatabaseName("idx_user_likes_liker_recent")
                .IsDescending(false, true);

            // Index for discovery exclusion queries
            builder.HasIndex(ul => ul.LikerUserId)
                .HasDatabaseName("idx_user_likes_liker_discovery");

            // Unique constraint to prevent duplicate likes
            builder.HasIndex(ul => new { ul.LikerUserId, ul.LikedUserId })
                .IsUnique()
                .HasDatabaseName("idx_user_likes_unique");

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
