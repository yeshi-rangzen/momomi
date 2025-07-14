using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Data.Configurations
{
    public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
    {
        public void Configure(EntityTypeBuilder<UserPreference> builder)
        {
            builder.HasKey(up => up.Id);

            builder.HasIndex(up => up.UserId)
                .IsUnique()
                .HasDatabaseName("idx_user_preferences_user_id");

            // Configure enum arrays with proper value comparers
            builder.Property(up => up.PreferredHeritage)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<HeritageType>(x)).ToList()
                        : new List<HeritageType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<HeritageType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredReligions)
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

            builder.Property(up => up.LanguagePreference)
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

            // SUBSCRIBER FILTER CONFIGURATIONS
            builder.Property(up => up.PreferredChildren)
                .HasConversion(
                    v => v != null ? String.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<ChildrenStatusType>(x)).ToList()
                            : new List<ChildrenStatusType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<ChildrenStatusType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredFamilyPlans)
               .HasConversion(
                   v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                   v => !string.IsNullOrEmpty(v)
                       ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(x => Enum.Parse<FamilyPlanType>(x)).ToList()
                       : new List<FamilyPlanType>()
               )
               .Metadata.SetValueComparer(new ValueComparer<List<FamilyPlanType>>(
                   (c1, c2) => c1!.SequenceEqual(c2!),
                   c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                   c => c.ToList()));

            builder.Property(up => up.PreferredDrugs)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<ViceFrequencyType>(x)).ToList()
                        : new List<ViceFrequencyType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<ViceFrequencyType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredSmoking)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<ViceFrequencyType>(x)).ToList()
                        : new List<ViceFrequencyType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<ViceFrequencyType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredMarijuana)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<ViceFrequencyType>(x)).ToList()
                        : new List<ViceFrequencyType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<ViceFrequencyType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredDrinking)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<ViceFrequencyType>(x)).ToList()
                        : new List<ViceFrequencyType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<ViceFrequencyType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.Property(up => up.PreferredEducationLevels)
                .HasConversion(
                    v => v != null ? string.Join(',', v.Select(x => x.ToString())) : null,
                    v => !string.IsNullOrEmpty(v)
                        ? v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<EducationLevelType>(x)).ToList()
                        : new List<EducationLevelType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<EducationLevelType>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Configure relationship
            builder.HasOne(up => up.User)
                .WithOne(u => u.Preferences)
                .HasForeignKey<UserPreference>(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
