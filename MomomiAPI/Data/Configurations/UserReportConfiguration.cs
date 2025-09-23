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

            builder.HasIndex(ur => ur.ReporterEmail)
                .HasDatabaseName("idx_user_reports_reporter");

            builder.HasIndex(ur => ur.ReportedEmail)
                .HasDatabaseName("idx_user_reports_reported");

            builder.HasIndex(ur => ur.Status)
                .HasDatabaseName("idx_user_reports_status");

            // Index for reason
            builder.HasIndex(ur => ur.Reason)
                .HasDatabaseName("idx_user_reports_reason");

            // Configure enum conversion
            builder.Property(ur => ur.Reason)
                .HasConversion<string>();
        }
    }
}
