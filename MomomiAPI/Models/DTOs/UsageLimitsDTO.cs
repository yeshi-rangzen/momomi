namespace MomomiAPI.Models.DTOs
{
    public class UsageLimitsDTO
    {
        // Likes
        public int LikesUsedToday { get; set; }
        public int MaxLikesPerDay { get; set; }

        // Super Likes
        public int SuperLikesUsedToday { get; set; }
        public int MaxSuperLikesPerDay { get; set; }

        // Ads (Free users only)
        public int AdsWatchedToday { get; set; }
        public int MaxAdsPerDay { get; set; }
    }
}
