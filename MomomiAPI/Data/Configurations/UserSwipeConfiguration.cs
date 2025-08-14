using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserSwipeConfiguration : IEntityTypeConfiguration<UserSwipe>
    {
        public void Configure(EntityTypeBuilder<UserSwipe> builder)
        {
            builder.HasKey(ul => ul.Id);

            // Composite index for match queries
            builder.HasIndex(ul => new { ul.SwiperUserId, ul.SwipedUserId })
                .HasDatabaseName("idx_user_swipes_composite_main");

            // For "users who like me" queries
            builder.HasIndex(ul => new { ul.SwiperUserId, ul.SwipeType })
                .HasDatabaseName("idx_user_swipes_received")
                .IncludeProperties(ul => new { ul.SwipedUserId, ul.CreatedAt });

            // For undo last swipe functionality
            builder.HasIndex(ul => new { ul.SwiperUserId, ul.CreatedAt })
                .HasDatabaseName("idx_user_swipes_recent")
                .IsDescending(false, true);

            // Unique constraint to prevent duplicate likes
            builder.HasIndex(ul => new { ul.SwiperUserId, ul.SwipedUserId })
                .IsUnique()
                .HasDatabaseName("idx_user_swipes_unique");

            builder.Property(ul => ul.SwipeType)
                .HasConversion<string>();

            // Configure explicit relationships to avoid ambiguity
            builder.HasOne(ul => ul.SwiperUser)
                .WithMany(u => u.SwipesGiven)
                .HasForeignKey(ul => ul.SwiperUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ul => ul.SwipedUser)
                .WithMany(u => u.SwipesReceived)
                .HasForeignKey(ul => ul.SwipedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
