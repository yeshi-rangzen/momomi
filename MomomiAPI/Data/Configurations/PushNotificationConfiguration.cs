using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class PushNotificationConfiguration : IEntityTypeConfiguration<PushNotification>
    {
        public void Configure(EntityTypeBuilder<PushNotification> builder)
        {
            builder.HasKey(pn => pn.Id);

            builder.HasIndex(pn => pn.UserId)
                .HasDatabaseName("idx_push_notifications_user_id");

            builder.HasIndex(pn => new { pn.UserId, pn.IsRead })
                .HasDatabaseName("idx_push_notifications_user_read");

            builder.HasIndex(pn => new { pn.IsSent, pn.CreatedAt })
                .HasDatabaseName("idx_push_notifications_sent_created");

            builder.Property(pn => pn.NotificationType)
                .HasConversion<string>();

            builder.HasOne(pn => pn.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(pn => pn.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
