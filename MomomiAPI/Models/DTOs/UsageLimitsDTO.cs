using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class UsageLimitsDTO
    {
        public SubscriptionType SubscriptionType { get; set; }

        // Likes
        public int LikesUsedToday { get; set; }
        public int MaxLikesPerDay { get; set; }
        public int RemainingLikes { get; set; }
        public int BonusLikesFromAds { get; set; }

        // Super Likes
        public int SuperLikesUsedToday { get; set; }
        public int MaxSuperLikesPerDay { get; set; }
        public int SuperLikesUsedThisWeek { get; set; }
        public int MaxSuperLikesPerWeek { get; set; }
        public int RemainingSuperLikes { get; set; }

        // Matches
        public int MatchesCount { get; set; }
        public int MaxMatches { get; set; }
        public int RemainingMatches { get; set; }

        // Ads (Free users only)
        public int AdsWatchedToday { get; set; }
        public int MaxAdsPerDay { get; set; }
        public int RemainingAds { get; set; }
    }
}
