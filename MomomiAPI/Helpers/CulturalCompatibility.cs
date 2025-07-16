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

            // Religion compatibility (25% weight)
            var religionScore = CalculateReligionCompatibility(user1, user2);
            totalScore += religionScore * 0.25;
            factors++;

            // Language compatibility (25% weight)
            var languageScore = CalculateLanguageCompatibility(user1, user2);
            totalScore += languageScore * 0.25;
            factors++;

            // Geographic/regional proximity (20% weight)
            var regionScore = CalculateRegionalCompatibility(user1, user2);
            totalScore += regionScore * 0.25;
            factors++;

            return factors > 0 ? totalScore : 50.0;
        }

        private static double CalculateHeritageCompatibility(User user1, User user2)
        {
            // Handle both single and multiple heritage values
            var user1Heritage = user1.Heritage ?? new List<HeritageType>();
            var user2Heritage = user2.Heritage ?? new List<HeritageType>();

            if (!user1Heritage.Any() || !user2Heritage.Any())
                return 50.0; // Neutral if no heritage specified

            // Perfect match if any heritage overlap
            var commonHeritage = user1Heritage.Intersect(user2Heritage).ToList();
            if (commonHeritage.Any())
                return 100.0;

            // Check mutual preferences
            var user1AcceptsUser2 = user1.Preferences?.PreferredHeritage?.Any(h => user2Heritage.Contains(h)) ?? false;
            var user2AcceptsUser1 = user2.Preferences?.PreferredHeritage?.Any(h => user1Heritage.Contains(h)) ?? false;

            if (user1AcceptsUser2 && user2AcceptsUser1)
                return 90.0;

            if (user1AcceptsUser2 || user2AcceptsUser1)
                return 70.0;

            // Related heritage groups (e.g., Tibetan-Buddhist cultures)
            var relatedGroups = GetRelatedHeritageGroups();
            if (AreHeritagesRelated(user1Heritage, user2Heritage, relatedGroups))
                return 60.0;

            return 30.0; // Different heritage, no preference match
        }

        private static double CalculateReligionCompatibility(User user1, User user2)
        {
            // Handle both single and multiple religion values
            var user1Religion = user1.Religion ?? new List<ReligionType>();
            var user2Religion = user2.Religion ?? new List<ReligionType>();

            if (!user1Religion.Any() || !user2Religion.Any())
                return 50.0; // Neutral if no religion specified

            // Perfect match if any religion overlap
            var commonReligion = user1Religion.Intersect(user2Religion).ToList();
            if (commonReligion.Any())
                return 100.0;

            // Check mutual preferences
            var user1AcceptsUser2 = user1.Preferences?.PreferredReligions?.Any(r => user2Religion.Contains(r)) ?? false;
            var user2AcceptsUser1 = user2.Preferences?.PreferredReligions?.Any(r => user1Religion.Contains(r)) ?? false;

            if (user1AcceptsUser2 && user2AcceptsUser1)
                return 90.0;
            if (user1AcceptsUser2 || user2AcceptsUser1)
                return 70.0;

            // Compatible religions (e.g., Buddhism and Hinduism)
            var compatibleReligions = GetCompatibleReligions();
            if (AreReligionsCompatible(user1Religion, user2Religion, compatibleReligions))
                return 60.0;

            return 40.0; // Different religions, no preference match
        }

        private static double CalculateLanguageCompatibility(User user1, User user2)
        {
            if (user1.LanguagesSpoken == null || user2.LanguagesSpoken == null)
                return 50.0; // Neutral score if no languages specified

            var commonLanguages = user1.LanguagesSpoken.Intersect(user2.LanguagesSpoken).ToList();

            if (!commonLanguages.Any())
                return 20.0; // No common languages

            var score = 0.0;

            // Base score for having common languages (40 points)
            score += 40.0;

            // Bonus for each additional common language (up to 30 points)
            score += Math.Min(30.0, (commonLanguages.Count - 1) * 10.0);

            // Cultural language bonus (up to 30 points)
            var culturalLanguages = GetCommonCulturalLanguages(user1.LanguagesSpoken, user2.LanguagesSpoken);
            score += Math.Min(30.0, culturalLanguages.Count * 15.0);

            // Regional language family bonus (up to 20 points)
            var regionalFamilyBonus = CalculateRegionalLanguageFamilyBonus(commonLanguages);
            score += Math.Min(20.0, regionalFamilyBonus);

            // Language preference matching bonus (up to 10 points)
            var preferenceBonus = CalculateLanguagePreferenceBonus(user1, user2);
            score += Math.Min(10.0, preferenceBonus);

            return Math.Min(100.0, score);
        }

        /// <summary>
        /// TODO: currently incomplete business logic
        /// Get culturally significant common languages
        /// </summary>
        private static List<LanguageType> GetCommonCulturalLanguages(List<LanguageType> languages1, List<LanguageType> languages2)
        {
            var culturalLanguages = new List<LanguageType>
            {
                // Major Tibetan language family
                LanguageType.Tibetan, LanguageType.Ladakhi, LanguageType.SpitiBhoti, LanguageType.Dzongkha,
                LanguageType.Bhoti, LanguageType.Monpa,
                LanguageType.Sherdukpen,
                
                // Nepali language family
                LanguageType.Nepali,
                LanguageType.Tamang, LanguageType.Gurung, LanguageType.Sherpa,
                LanguageType.Limbu, LanguageType.Rai, LanguageType.Magar, LanguageType.Thakali, LanguageType.Newar,
                
                // Northeast Indian languages
                LanguageType.Nyishi,
                //LanguageType.Meiteilon, LanguageType.Mizo, LanguageType.Bodo, LanguageType.Karbi,
                //LanguageType.Ao, LanguageType.Angami, LanguageType.Konyak, 
                LanguageType.Adi, LanguageType.Apatani, LanguageType.Mishmi,
                
                // Other significant regional languages
                LanguageType.Lepcha, LanguageType.Kinnauri,
                LanguageType.Balti,
            };

            return languages1.Intersect(languages2).Where(lang => culturalLanguages.Contains(lang)).ToList();
        }

        /// <summary>
        /// Calculate bonus for language family relationships
        /// </summary>
        private static double CalculateRegionalLanguageFamilyBonus(List<LanguageType> commonLanguages)
        {
            var languageFamilies = new Dictionary<string, List<LanguageType>>
            {
                ["Tibetic"] = new() {
                    LanguageType.Tibetan, LanguageType.Ladakhi, LanguageType.SpitiBhoti,
                    LanguageType.Dzongkha, LanguageType.Bhoti, LanguageType.Nyishi, 
                    //LanguageType.Balti
                },
                ["Tamangic"] = new() {
                    LanguageType.Tamang, LanguageType.Gurung, LanguageType.Thakali
                },
                ["Kiranti"] = new() {
                    LanguageType.Limbu, LanguageType.Rai
                },
                //["Kuki-Chin"] = new() {
                //    LanguageType.Mizo, LanguageType.Kuki, LanguageType.Chin, LanguageType.Hmar,
                //    LanguageType.Zou, LanguageType.Paite, LanguageType.Thadou
                //},
                //["Naga"] = new() {
                //    LanguageType.Ao, LanguageType.Angami, LanguageType.Konyak, LanguageType.Lotha,
                //    LanguageType.Sema, LanguageType.Phom, LanguageType.Chang, LanguageType.Chakhesang
                //},
                ["Tani"] = new() {
                    LanguageType.Nyishi, LanguageType.Adi, LanguageType.Apatani, LanguageType.Tagin
                },
                //["Bodo-Garo"] = new() {
                //    LanguageType.Bodo, LanguageType.Dimasa, LanguageType.Karbi, LanguageType.Tiwa
                //}
            };

            var bonus = 0.0;
            foreach (var family in languageFamilies.Values)
            {
                var familyMatches = commonLanguages.Intersect(family).Count();
                if (familyMatches > 1)
                {
                    bonus += 10.0; // Bonus for sharing multiple languages from the same family
                }
                else if (familyMatches == 1)
                {
                    bonus += 5.0; // Smaller bonus for sharing one language from a family
                }
            }

            return bonus;
        }

        /// <summary>
        /// Calculate bonus for matching language preferences
        /// </summary>
        private static double CalculateLanguagePreferenceBonus(User user1, User user2)
        {
            var bonus = 0.0;

            // Check if user1's spoken languages match user2's preferences
            if (user2.Preferences?.LanguagePreference != null && user1.LanguagesSpoken != null)
            {
                var user1MatchesUser2Prefs = user1.LanguagesSpoken.Intersect(user2.Preferences.LanguagePreference).Any();
                if (user1MatchesUser2Prefs) bonus += 5.0;
            }

            // Check if user2's spoken languages match user1's preferences
            if (user1.Preferences?.LanguagePreference != null && user2.LanguagesSpoken != null)
            {
                var user2MatchesUser1Prefs = user2.LanguagesSpoken.Intersect(user1.Preferences.LanguagePreference).Any();
                if (user2MatchesUser1Prefs) bonus += 5.0;
            }

            return bonus;
        }

        private static double CalculateRegionalCompatibility(User user1, User user2)
        {
            // Enhanced regional mapping for Himalayan/Northeast regions
            var himalayanRegions = new Dictionary<HeritageType, string[]>
            {
                { HeritageType.Tibetan, new[] { "Tibet", "Ladakh", "Sikkim", "Bhutan" } },
                { HeritageType.Nepali, new[] { "Nepal", "Sikkim", "Darjeeling", "Bhutan" } },
                { HeritageType.Bhutanese, new[] { "Bhutan", "Sikkim", "Arunachal" } },
                { HeritageType.Ladakhi, new[] { "Ladakh", "Tibet", "Himachal" } },
                { HeritageType.Sikkimese, new[] { "Sikkim", "Nepal", "Bhutan", "Darjeeling" } },
                { HeritageType.Arunachali, new[] { "Arunachal", "Assam", "Bhutan", "Tibet" } },
                //{ HeritageType.Assamese, new[] { "Assam", "Arunachal", "Nagaland", "Meghalaya" } },
                //{ HeritageType.Naga, new[] { "Nagaland", "Manipur", "Assam", "Arunachal" } },
                //{ HeritageType.Manipuri, new[] { "Manipur", "Nagaland", "Mizoram", "Assam" } },
                //{ HeritageType.Mizo, new[] { "Mizoram", "Manipur", "Tripura", "Assam" } }
            };

            var user1Heritage = user1.Heritage ?? new List<HeritageType>();
            var user2Heritage = user2.Heritage ?? new List<HeritageType>();

            if (!user1Heritage.Any() || !user2Heritage.Any())
                return 50.0;

            // Direct heritage match
            if (user1Heritage.Intersect(user2Heritage).Any())
                return 100.0;

            // Check regional connections
            var maxRegionalScore = 0.0;
            foreach (var h1 in user1Heritage)
            {
                foreach (var h2 in user2Heritage)
                {
                    if (himalayanRegions.ContainsKey(h1) && himalayanRegions.ContainsKey(h2))
                    {
                        var regions1 = himalayanRegions[h1];
                        var regions2 = himalayanRegions[h2];

                        var commonRegions = regions1.Intersect(regions2).Count();
                        var regionalScore = Math.Min(80.0, commonRegions * 20.0);
                        maxRegionalScore = Math.Max(maxRegionalScore, regionalScore);
                    }
                }
            }

            return Math.Max(50.0, maxRegionalScore);
        }

        private static Dictionary<HeritageType, List<HeritageType>> GetRelatedHeritageGroups()
        {
            return new Dictionary<HeritageType, List<HeritageType>>
            {
                { HeritageType.Tibetan, new List<HeritageType> { HeritageType.Ladakhi, HeritageType.Bhutanese, HeritageType.Sikkimese } },
                { HeritageType.Nepali, new List<HeritageType> { HeritageType.Sikkimese, HeritageType.Bhutanese } },
                { HeritageType.Bhutanese, new List<HeritageType> { HeritageType.Tibetan, HeritageType.Sikkimese, HeritageType.Arunachali } },
                { HeritageType.Ladakhi, new List<HeritageType> { HeritageType.Tibetan, 
                    //HeritageType.Himachali 
                } },
                //{ HeritageType.Naga, new List<HeritageType> { HeritageType.Manipuri, HeritageType.Assamese, HeritageType.Arunachali } },
                //{ HeritageType.Manipuri, new List<HeritageType> { HeritageType.Naga, HeritageType.Mizo, HeritageType.Assamese } },
                { HeritageType.Arunachali, new List<HeritageType> { 
                    //HeritageType.Assamese, 
                    HeritageType.Tibetan, HeritageType.Bhutanese } }
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

        private static bool AreHeritagesRelated(List<HeritageType> heritage1, List<HeritageType> heritage2,
             Dictionary<HeritageType, List<HeritageType>> relatedGroups)
        {
            foreach (var h1 in heritage1)
            {
                foreach (var h2 in heritage2)
                {
                    if (relatedGroups.ContainsKey(h1) && relatedGroups[h1].Contains(h2))
                        return true;
                    if (relatedGroups.ContainsKey(h2) && relatedGroups[h2].Contains(h1))
                        return true;
                }
            }
            return false;
        }

        private static bool AreReligionsCompatible(List<ReligionType> religion1, List<ReligionType> religion2,
            Dictionary<ReligionType, List<ReligionType>> compatibleReligions)
        {
            foreach (var r1 in religion1)
            {
                foreach (var r2 in religion2)
                {
                    if (compatibleReligions.ContainsKey(r1) && compatibleReligions[r1].Contains(r2))
                        return true;
                    if (compatibleReligions.ContainsKey(r2) && compatibleReligions[r2].Contains(r1))
                        return true;
                }
            }
            return false;
        }
    }
}
