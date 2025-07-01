using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Helpers
{
    public class CulturalCompatibility
    {
        /// <summary>
        /// Calculates cultural compatibility score between two users (0-100)
        /// Higher score indicates better cultural compatibility
        /// </summary>
        public static double CalculateCompatibilityScore(User user1, User user2)
        {
            if (user1.Preferences == null || user2.Preferences == null)
            {
                return 50.0; // Default neutral score
            }

            double totalScore = 0;
            int factors = 0;

            // Heritage compatibility (30% weight)
            var heritageScore = CalculateHeritageCompatibility(user1, user2);
            totalScore += heritageScore * 0.30;
            factors++;

            // Religion compatibility (20% weight)
            var religionScore = CalculateReligionCompatibility(user1, user2);
            totalScore += religionScore * 0.20;
            factors++;

            // Language compatibility (20% weight)
            var languageScore = CalculateLanguageCompatibility(user1, user2);
            totalScore += languageScore * 0.20;
            factors++;

            // Cultural importance alignment (15% weight)
            var culturalScore = CalculateCulturalImportanceAlignment(user1, user2);
            totalScore += culturalScore * 0.15;
            factors++;

            // Geographic/regional proximity (10% weight)
            var regionScore = CalculateRegionalCompatibility(user1, user2);
            totalScore += regionScore * 0.10;
            factors++;

            return factors > 0 ? totalScore : 50.0;
        }

        private static double CalculateHeritageCompatibility(User user1, User user2)
        {
            // Perfect match if same heritage
            if (user1.Heritage == user2.Heritage && user1.Heritage.HasValue)
                return 100.0;

            // Check mutual preferences
            var user1AcceptsUser2 = user1.Preferences?.PreferredHeritage?.Contains(user2.Heritage ?? HeritageType.Other) ?? false;
            var user2AcceptsUser1 = user2.Preferences?.PreferredHeritage?.Contains(user1.Heritage ?? HeritageType.Other) ?? false;

            if (user1AcceptsUser2 && user2AcceptsUser1)
                return 90.0;

            if (user1AcceptsUser2 || user2AcceptsUser1)
                return 70.0;

            // Related heritage groups (e.g., Tibetan-Buddhist cultures)
            var relatedGroups = GetRelatedHeritageGroups();
            if (AreHeritagesRelated(user1.Heritage, user2.Heritage, relatedGroups))
                return 60.0;

            return 30.0; // Different heritage, no preference match
        }

        private static double CalculateReligionCompatibility(User user1, User user2)
        {
            // Perfect match if same religion
            if (user1.Religion == user2.Religion && user1.Religion.HasValue) return 100.0;

            // Check mutual preferences
            var user1AcceptsUser2 = user1.Preferences?.PreferredReligions?.Contains(user2.Religion ?? ReligionType.Other) ?? false;
            var user2AcceptsUser1 = user2.Preferences?.PreferredReligions?.Contains(user1.Religion ?? ReligionType.Other) ?? false;

            if (user1AcceptsUser2 && user2AcceptsUser1)
                return 90.0;
            if (user1AcceptsUser2 || user2AcceptsUser1)
                return 70.0;

            // Compatibile religions (e.g., Buddhism and Hinduism)
            var compatibleRelgions = GetCompatibleReligions();
            if (AreReligionsCompatible(user1.Religion, user2.Religion, compatibleRelgions))
                return 60.0;

            return 40.0; // Different religions, no preference match
        }

        private static double CalculateLanguageCompatibility(User user1, User user2)
        {
            if (user1.LanguagesSpoken == null || user2.LanguagesSpoken == null)
                return 50.0; // Neutral score if no languages specified

            // Check for common languages
            var commonLanguages = user1.LanguagesSpoken.Intersect(user2.LanguagesSpoken).Count();
            var totalUniqueLanguages = user1.LanguagesSpoken.Union(user2.LanguagesSpoken).Count();

            if (commonLanguages == 0)
                return 20.0; // No common languages

            return commonLanguages / totalUniqueLanguages * 100.0; // Percentage of common languages
        }

        private static double CalculateCulturalImportanceAlignment(User user1, User user2)
        {
            // This would be expanded with actual regional data
            var himalayanRegions = new Dictionary<HeritageType, string[]>
            {
                { HeritageType.Tibetan, new[] { "Tibet", "Ladakh", "Sikkim" } },
                { HeritageType.Nepali, new[] { "Nepal", "Sikkim", "Darjeeling" } },
                { HeritageType.Bhutanese, new[] { "Bhutan", "Sikkim" } },
                { HeritageType.Ladakhi, new[] { "Ladakh", "Tibet" } },
                { HeritageType.Sikkimese, new[] { "Sikkim", "Nepal", "Bhutan" } }
            };

            if (user1.Heritage == user2.Heritage)
                return 100.0;

            // Check if regions are adjacent or culturally connected
            if (himalayanRegions.ContainsKey(user1.Heritage ?? HeritageType.Other) &&
                himalayanRegions.ContainsKey(user2.Heritage ?? HeritageType.Other))
            {
                var regions1 = himalayanRegions[user1.Heritage ?? HeritageType.Other];
                var regions2 = himalayanRegions[user2.Heritage ?? HeritageType.Other];

                if (regions1.Intersect(regions2).Any())
                    return 80.0;
            }

            return 50.0; // Neutral score if no strong cultural alignment
        }

        private static double CalculateRegionalCompatibility(User user1, User user2)
        {
            // This would be expanded with actual regional data
            var himalayanRegions = new Dictionary<HeritageType, string[]>
            {
                { HeritageType.Tibetan, new[] { "Tibet", "Ladakh", "Sikkim" } },
                { HeritageType.Nepali, new[] { "Nepal", "Sikkim", "Darjeeling" } },
                { HeritageType.Bhutanese, new[] { "Bhutan", "Sikkim" } },
                { HeritageType.Ladakhi, new[] { "Ladakh", "Tibet" } },
                { HeritageType.Sikkimese, new[] { "Sikkim", "Nepal", "Bhutan" } }
            };

            if (user1.Heritage == user2.Heritage)
                return 100.0;

            // Check if regions are adjacent or culturally connected
            if (himalayanRegions.ContainsKey(user1.Heritage ?? HeritageType.Other) &&
                himalayanRegions.ContainsKey(user2.Heritage ?? HeritageType.Other))
            {
                var regions1 = himalayanRegions[user1.Heritage ?? HeritageType.Other];
                var regions2 = himalayanRegions[user2.Heritage ?? HeritageType.Other];

                if (regions1.Intersect(regions2).Any())
                    return 80.0;
            }

            return 50.0;
        }

        private static Dictionary<HeritageType, List<HeritageType>> GetRelatedHeritageGroups()
        {
            return new Dictionary<HeritageType, List<HeritageType>>
            {
                { HeritageType.Tibetan, new List<HeritageType> { HeritageType.Ladakhi, HeritageType.Bhutanese } },
                { HeritageType.Nepali, new List<HeritageType> { HeritageType.Sikkimese } },
                { HeritageType.Bhutanese, new List<HeritageType> { HeritageType.Tibetan, HeritageType.Sikkimese } },
                { HeritageType.Ladakhi, new List<HeritageType> { HeritageType.Tibetan } },
                { HeritageType.Naga, new List<HeritageType> { HeritageType.Manipuri, HeritageType.Assamese } },
                { HeritageType.Manipuri, new List<HeritageType> { HeritageType.Naga, HeritageType.Mizo } }
            };
        }

        private static Dictionary<ReligionType, List<ReligionType>> GetCompatibleReligions()
        {
            return new Dictionary<ReligionType, List<ReligionType>>
            {
                { ReligionType.Buddhism, new List<ReligionType> { ReligionType.Spiritual, ReligionType.Hindu } },
                { ReligionType.Hindu, new List<ReligionType> { ReligionType.Buddhism, ReligionType.Spiritual } },
                { ReligionType.Spiritual, new List<ReligionType> { ReligionType.Buddhism, ReligionType.Hindu, ReligionType.Agnostic } },
                { ReligionType.Agnostic, new List<ReligionType> { ReligionType.Spiritual, ReligionType.Atheism } },
                { ReligionType.Animism, new List<ReligionType> { ReligionType.Spiritual, ReligionType.DonyiPolo } },
                { ReligionType.DonyiPolo, new List<ReligionType> { ReligionType.Animism, ReligionType.Spiritual } }
            };
        }

        private static bool AreHeritagesRelated(HeritageType? heritage1, HeritageType? heritage2,
            Dictionary<HeritageType, List<HeritageType>> relatedGroups)
        {
            if (!heritage1.HasValue || !heritage2.HasValue) return false;

            return relatedGroups.ContainsKey(heritage1.Value) &&
                   relatedGroups[heritage1.Value].Contains(heritage2.Value);
        }

        private static bool AreReligionsCompatible(ReligionType? religion1, ReligionType? religion2,
            Dictionary<ReligionType, List<ReligionType>> compatibleReligions)
        {
            if (!religion1.HasValue || !religion2.HasValue) return false;

            return compatibleReligions.ContainsKey(religion1.Value) &&
                   compatibleReligions[religion1.Value].Contains(religion2.Value);
        }
    }
}
