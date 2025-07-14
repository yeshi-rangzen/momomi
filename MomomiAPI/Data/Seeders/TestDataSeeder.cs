using Microsoft.EntityFrameworkCore;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Data.Seeders
{
    public static class TestDataSeeder
    {
        public static async Task SeedTestDataAsync(MomomiDbContext dbContext)
        {
            if (await dbContext.Users.AnyAsync())
                return; // Data already seeded

            var testUsers = new List<User> {
                new() {
                    Id = Guid.NewGuid(),
                    SupabaseUid = Guid.NewGuid(),
                    Email = "tenzin@example.com",
                    FirstName = "Tenzin",
                    LastName = "Norbu",
                    DateOfBirth = new DateTime(1995, 3, 15),
                    Gender = GenderType.Male,
                    InterestedIn = GenderType.Female,
                    Heritage = [HeritageType.Tibetan],
                    Religion = [ReligionType.Buddhism],
                    LanguagesSpoken = [LanguageType.Tibetan, LanguageType.English, LanguageType.Hindi],
                    Bio = "Traditional Tibetan culture enthusiast. Love mountains and meditation.",
                    EducationLevel = EducationLevelType.Undergrad,
                    Occupation = "Software Developer",
                    HeightCm = 175,
                    Latitude = 27.3389m,
                    Longitude = 88.6065m, // Gangtok, Sikkim
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new() {
                    Id = Guid.NewGuid(),
                    SupabaseUid = Guid.NewGuid(),
                    Email = "dolma@example.com",
                    FirstName = "Dolma",
                    LastName = "Sherpa",
                    DateOfBirth = new DateTime(1997, 8, 22),
                    Gender = GenderType.Female,
                    InterestedIn = GenderType.Male,
                    Heritage = [HeritageType.Nepali],
                    Religion = [ReligionType.Buddhism],
                    LanguagesSpoken = [LanguageType.Nepali, LanguageType.English],
                    Bio = "Mountain lover from the Everest region. Love hiking and traditional music.",
                    EducationLevel = EducationLevelType.Postgrad,
                    Occupation = "Teacher",
                    HeightCm = 160,
                    Latitude = 27.7172m,
                    Longitude = 85.3240m, // Kathmandu, Nepal
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }, new() {
                    Id = Guid.NewGuid(),
                    SupabaseUid = Guid.NewGuid(),
                    Email = "karma@example.com",
                    FirstName = "Karma",
                    LastName = "Wangchuk",
                    DateOfBirth = new DateTime(1993, 11, 8),
                    Gender = GenderType.Male,
                    InterestedIn = GenderType.Female,
                    Heritage = [HeritageType.Bhutanese],
                    Religion = [ReligionType.Buddhism],
                    LanguagesSpoken = [LanguageType.Dzongkha, LanguageType.English, LanguageType.Hindi],
                    Bio = "From the Land of Thunder Dragon. Love archery and traditional festivals.",
                    EducationLevel = EducationLevelType.Undergrad,
                    Occupation = "Government Officer",
                    HeightCm = 170,
                    Latitude = 27.4728m,
                    Longitude = 89.6390m, // Thimphu, Bhutan
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            dbContext.Users.AddRange(testUsers);
            await dbContext.SaveChangesAsync();

            // Add preferences for test users
            var preferences = new List<UserPreference>
            {
                new() {
                    UserId = testUsers[0].Id,
                    PreferredHeritage = [HeritageType.Tibetan, HeritageType.Nepali, HeritageType.Bhutanese],
                    PreferredReligions = [ReligionType.Buddhism, ReligionType.Spiritual],
                    LanguagePreference = [LanguageType.Tibetan, LanguageType.Nepali],
                    CreatedAt = DateTime.UtcNow
                },
                new() {
                    UserId = testUsers[1].Id,
                    PreferredHeritage = [HeritageType.Nepali, HeritageType.Tibetan, HeritageType.Sikkimese],
                    PreferredReligions = [ReligionType.Buddhism, ReligionType.Hindu],
                    LanguagePreference = [LanguageType.Nepali, LanguageType.English],
                    CreatedAt = DateTime.UtcNow
                },
                new() {
                    UserId = testUsers[2].Id,
                    PreferredHeritage = [HeritageType.Bhutanese, HeritageType.Tibetan],
                    PreferredReligions = [ReligionType.Buddhism],
                    LanguagePreference = [LanguageType.Dzongkha, LanguageType.Tibetan],
                    CreatedAt = DateTime.UtcNow
                }
            };

            dbContext.UserPreferences.AddRange(preferences);
            await dbContext.SaveChangesAsync();
        }
    }
}
