using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
    {
        public void Configure(EntityTypeBuilder<UserBlock> builder)
        {
            builder.HasKey(ub => ub.Id);

            builder.HasIndex(ub => new { ub.BlockerUserId, ub.BlockedUserId })
                .IsUnique()
                .HasDatabaseName("idx_user_blocks_unique");

            builder.HasIndex(ub => ub.BlockerUserId)
                .HasDatabaseName("idx_user_blocks_blocker");

            builder.HasIndex(ub => ub.BlockedUserId)
                .HasDatabaseName("idx_user_blocks_blocked");

            builder.HasOne(ub => ub.BlockerUser)
                .WithMany(u => u.BlocksMade)
                .HasForeignKey(ub => ub.BlockerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ub => ub.BlockedUser)
                .WithMany(u => u.BlocksReceived)
                .HasForeignKey(ub => ub.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
