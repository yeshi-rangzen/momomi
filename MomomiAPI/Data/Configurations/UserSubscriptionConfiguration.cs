using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
    {
        public void Configure(EntityTypeBuilder<UserSubscription> builder)
        {
            builder.HasKey(us => us.Id);

            builder.HasIndex(us => us.UserId)
                .IsUnique()
                .HasDatabaseName("idx_user_subscriptions_user_id");

            builder.HasIndex(us => new { us.IsActive, us.ExpiresAt })
                .HasDatabaseName("idx_user_subscriptions_active_expires");

            builder.Property(us => us.SubscriptionType)
                .HasConversion<string>();

            builder.HasOne(us => us.User)
                .WithOne(u => u.Subscription)
                .HasForeignKey<UserSubscription>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
