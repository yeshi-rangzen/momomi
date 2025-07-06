using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
    {
        public void Configure(EntityTypeBuilder<UserReport> builder)
        {
            builder.HasKey(ur => ur.Id);

            builder.HasIndex(ur => ur.ReporterId)
                .HasDatabaseName("idx_user_reports_reporter");

            builder.HasIndex(ur => ur.ReportedId)
                .HasDatabaseName("idx_user_reports_reported");

            builder.HasIndex(ur => ur.Status)
                .HasDatabaseName("idx_user_reports_status");

            // Index for reason
            builder.HasIndex(ur => ur.Reason)
                .HasDatabaseName("idx_user_reports_reason");

            // Configure enum conversion
            builder.Property(ur => ur.Reason)
                .HasConversion<string>();

            // Configure explicit relationships to avoid ambiguity
            builder.HasOne(ur => ur.Reporter)
                .WithMany(u => u.ReportsMade)
                .HasForeignKey(ur => ur.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ur => ur.Reported)
                .WithMany(u => u.ReportsReceived)
                .HasForeignKey(ur => ur.ReportedId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
