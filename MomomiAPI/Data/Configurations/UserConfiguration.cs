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

            // Main discovery composite index
            builder.HasIndex(u => new { u.IsActive, u.IsDiscoverable, u.Gender, u.InterestedIn })
                .HasDatabaseName("idx_users_discovery_main")
                .HasFilter("is_active = true AND is_discoverable = true")
                .IncludeProperties(u => new
                {
                    u.DateOfBirth,
                    u.Heritage,
                    u.Latitude,
                    u.Longitude,
                    u.EnableGlobalDiscovery,
                    u.IsGloballyDiscoverable,
                    u.LastActive
                });

            // Global discovery index
            builder.HasIndex(u => new { u.IsActive, u.IsGloballyDiscoverable, u.EnableGlobalDiscovery, u.Gender })
                .HasDatabaseName("idx_users_global_discovery")
                .HasFilter("is_active = true AND is_globally_discoverable = true")
                .IncludeProperties(u => new { u.DateOfBirth, u.Heritage, u.InterestedIn, u.LastActive });

            // Location based discovery (for PostGIS compatibility)
            builder.HasIndex(u => new { u.IsActive, u.IsDiscoverable, u.Latitude, u.Longitude })
                .HasDatabaseName("idx_users_location_discovery")
                .HasFilter("is_active = true AND is_discoverable = true AND latitude IS NOT NULL AND longitude IS NOT NULL");

            // Age-base filtering index
            builder.HasIndex(u => new { u.IsActive, u.DateOfBirth, u.Gender })
                .HasDatabaseName("idx_users_age_gender")
                .HasFilter("is_active = true");

            // Unique constraint for Supabase UID
            builder.HasIndex(u => u.SupabaseUid)
                .IsUnique()
                .HasDatabaseName("idx_users_supabase_uid");

            // Last active for relevance scoring
            builder.HasIndex(u => new { u.IsActive, u.LastActive })
                .HasDatabaseName("idx_users_active_last")
                .HasFilter("is_active = true")
                .IsDescending(false, true);

            // Configure enum conversions
            builder.Property(u => u.Gender).HasConversion<string>();
            builder.Property(u => u.InterestedIn).HasConversion<string>();
            builder.Property(u => u.Children).HasConversion<string>();
            builder.Property(u => u.FamilyPlan).HasConversion<string>();
            builder.Property(u => u.Drugs).HasConversion<string>();
            builder.Property(u => u.Smoking).HasConversion<string>();
            builder.Property(u => u.Marijuana).HasConversion<string>();
            builder.Property(u => u.Drinking).HasConversion<string>();
            builder.Property(u => u.EducationLevel).HasConversion<string>();

            // List Properties
            builder.Property(u => u.Heritage)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => ParseEnumList<HeritageType>(v)
                )
                .Metadata.SetValueComparer(new ValueComparer<List<HeritageType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(u => u.Religion)
               .HasConversion(
                   v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => ParseEnumList<ReligionType>(v)
               )
               .Metadata.SetValueComparer(new ValueComparer<List<ReligionType>>(
                   (c1, c2) => c1!.SequenceEqual(c2!),
                   c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                   c => c.ToList()));

            builder.Property(u => u.LanguagesSpoken)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                   v => ParseEnumList<LanguageType>(v)
                )
                .Metadata.SetValueComparer(new ValueComparer<List<LanguageType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Configure default values
            builder.Property(u => u.IsDiscoverable).HasDefaultValue(true);
            builder.Property(u => u.EnableGlobalDiscovery).HasDefaultValue(false);
            builder.Property(u => u.IsGloballyDiscoverable).HasDefaultValue(false);

            // Configure relationships - Photos
            builder.HasMany(u => u.Photos)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationships - Likes Given
            builder.HasMany(u => u.SwipesGiven)
                .WithOne(l => l.SwiperUser)
                .HasForeignKey(l => l.SwiperUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships - Likes Received
            builder.HasMany(u => u.SwipesReceived)
                .WithOne(l => l.SwipedUser)
                .HasForeignKey(l => l.SwipedUserId)
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

            // Configure relationships - Preferences (One-to-One)
            builder.HasOne(u => u.Preferences)
                .WithOne(p => p.User)
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        // Helper method to clean and parse enum values
        private static List<T> ParseEnumList<T>(string? value) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value))
                return [];

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(x => Enum.Parse<T>(x.Trim()))
                       .ToList();
        }
    }
}
