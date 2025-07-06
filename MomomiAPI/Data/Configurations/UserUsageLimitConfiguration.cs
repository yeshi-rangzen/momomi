using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserUsageLimitConfiguration : IEntityTypeConfiguration<UserUsageLimit>
    {
        public void Configure(EntityTypeBuilder<UserUsageLimit> builder)
        {
            builder.HasKey(uul => uul.Id);

            builder.HasIndex(uul => uul.UserId)
                .IsUnique()
                .HasDatabaseName("idx_user_usage_limits_user_id");

            builder.HasIndex(uul => uul.LastResetDate)
                .HasDatabaseName("idx_user_usage_limits_reset_date");

            builder.HasOne(uul => uul.User)
                .WithOne(u => u.UsageLimit)
                .HasForeignKey<UserUsageLimit>(uul => uul.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
