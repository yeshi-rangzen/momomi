using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.HasIndex(u => u.SupabaseUid)
                .IsUnique()
                .HasDatabaseName("idx_users_supabase_uid");

            builder.HasIndex(u => new { u.Latitude, u.Longitude })
                .HasDatabaseName("idx_users_location")
                .HasFilter("is_active = true");

            builder.HasIndex(u => new { u.IsActive, u.LastActive })
                .HasDatabaseName("idx_users_active")
                .HasFilter("is_active = true");

            builder.HasIndex(u => u.Heritage)
                .HasDatabaseName("idx_users_heritage")
                .HasFilter("is_active = true");

            builder.HasIndex(u => u.DateOfBirth)
                .HasDatabaseName("idx_users_age")
                .HasFilter("is_active = true");

            builder.HasIndex(u => u.EnableGlobalDiscovery)
               .HasDatabaseName("idx_users_global_discovery")
               .HasFilter("is_active = true");

            // Configure enum conversions
            builder.Property(u => u.Gender)
                .HasConversion<string>();

            builder.Property(u => u.InterestedIn)
                .HasConversion<string>();

            builder.Property(u => u.Heritage)
                .HasConversion<string>();

            builder.Property(u => u.Religion)
                .HasConversion<string>();

            // Configure EnableGlobalDiscovery column
            builder.Property(u => u.EnableGlobalDiscovery)
                .HasColumnName("enable_global_discovery")
                .HasDefaultValue(true)
                .IsRequired();

            // Configure JSON column for languages with value comparer
            builder.Property(u => u.LanguagesSpoken)
                .HasColumnType("text[]")
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Configure relationships - Photos
            builder.HasMany(u => u.Photos)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationships - Likes Given
            builder.HasMany(u => u.LikesGiven)
                .WithOne(l => l.LikerUser)
                .HasForeignKey(l => l.LikerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Likes Received
            builder.HasMany(u => u.LikesReceived)
                .WithOne(l => l.LikedUser)
                .HasForeignKey(l => l.LikedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Conversations as User1
            builder.HasMany(u => u.ConversationsAsUser1)
                .WithOne(c => c.User1)
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Conversations as User2
            builder.HasMany(u => u.ConversationsAsUser2)
                .WithOne(c => c.User2)
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Messages Sent
            builder.HasMany(u => u.MessagesSent)
                .WithOne(m => m.Sender)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Reports Made
            builder.HasMany(u => u.ReportsMade)
                .WithOne(r => r.Reporter)
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Reports Received
            builder.HasMany(u => u.ReportsReceived)
                .WithOne(r => r.Reported)
                .HasForeignKey(r => r.ReportedId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Preferences (One-to-One)
            builder.HasOne(u => u.Preferences)
                .WithOne(p => p.User)
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
