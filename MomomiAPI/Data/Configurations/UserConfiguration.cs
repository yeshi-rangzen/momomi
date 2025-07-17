using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

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

            builder.HasIndex(u => u.IsDiscoverable)
               .HasDatabaseName("idx_users_discoverable")
               .HasFilter("is_active = true");

            builder.HasIndex(u => u.IsGloballyDiscoverable)
               .HasDatabaseName("idx_users_globally_discoverable")
               .HasFilter("is_active = true");

            // Configure enum conversions
            builder.Property(u => u.Gender)
                .HasConversion<string>();

            builder.Property(u => u.InterestedIn)
                .HasConversion<string>();

            builder.Property(u => u.Children)
                .HasConversion<string>();

            builder.Property(u => u.FamilyPlan)
                .HasConversion<string>();

            builder.Property(u => u.Drugs)
                .HasConversion<string>();

            builder.Property(u => u.Smoking)
                .HasConversion<string>();

            builder.Property(u => u.Marijuana)
                .HasConversion<string>();

            builder.Property(u => u.Drinking)
                .HasConversion<string>();

            builder.Property(u => u.EducationLevel)
                .HasConversion<string>();

            builder.Property(u => u.Heritage)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                    ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => Enum.Parse<HeritageType>(x.Trim(), true)).ToList()
                    : new List<HeritageType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<HeritageType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(u => u.Religion)
               .HasConversion(
                   v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                   v => !string.IsNullOrEmpty(v)
                       ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(x => Enum.Parse<ReligionType>(x)).ToList()
                       : new List<ReligionType>()
               )
               .Metadata.SetValueComparer(new ValueComparer<List<ReligionType>>(
                   (c1, c2) => c1!.SequenceEqual(c2!),
                   c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                   c => c.ToList()));

            builder.Property(u => u.LanguagesSpoken)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<LanguageType>(x)).ToList()
                        : new List<LanguageType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<LanguageType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Configure EnableGlobalDiscovery column
            builder.Property(u => u.EnableGlobalDiscovery)
                .HasColumnName("enable_global_discovery")
                .HasDefaultValue(true)
                .IsRequired();

            // Configure IsDiscoverable column
            builder.Property(u => u.IsDiscoverable)
                .HasColumnName("is_discoverable")
                .HasDefaultValue(true)
                .IsRequired();

            builder.Property(u => u.IsGloballyDiscoverable)
                .HasColumnName("is_globally_discoverable")
                .HasDefaultValue(true)
                .IsRequired();

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
