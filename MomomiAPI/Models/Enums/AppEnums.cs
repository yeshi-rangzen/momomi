
namespace MomomiAPI.Models.Enums
{
    public enum GenderType
    {
        Male,
        Female,
        NonBinary,
        Other
    }
    public enum HeritageType
    {
        Arunachali,
        Assamese,
        Bhutanese,
        Himachali,
        Ladakhi,
        Manipuri,
        Mizo,
        Naga,
        Nepali,
        Sikkimese,
        Tibetan,
        Tripuri,
        Uttarakhandi,
        Other
    }
    public enum ReligionType
    {
        Agnostic,
        Animism,
        Atheism,
        Buddhism,
        Christian,
        DonyiPolo,
        Hindu,
        Islam,
        Spiritual,
        Other
    }

    public enum SubscriptionType
    {
        Free,
        Premium
    }

    public enum LikeType
    {
        Regular,
        SuperLike
    }

    public enum ReportReason
    {
        Inappropriate,
        Spam,
        FakeProfile,
        Harassment,
        Underage,
        Other
    }

    public enum NotificationType
    {
        Match,
        Message,
        SuperLike,
        ProfileView
    }
}