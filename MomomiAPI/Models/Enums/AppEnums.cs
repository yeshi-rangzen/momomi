
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
        //Assamese,
        //Bodo,
        Bhutanese,
        //Himachali,
        Ladakhi,
        Lepcha,
        //Manipuri,
        //Mizo,
        Monpa,
        //Naga,
        Nepali,
        Sherpa,
        Sikkimese,
        //Tani,
        Tibetan,
        //Tripuri,
        //Uttarakhandi,
        //KukiChin,
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

    public enum ChildrenStatusType
    {
        DontHaveChildren,
        HaveChildren,
        PreferNotToSay
    }

    public enum FamilyPlanType
    {
        DontWantChildren,
        WantChildren,
        OpenToChildren,
        NotSure,
        PreferNotToSay
    }

    public enum ViceFrequencyType
    {
        Yes,
        No,
        Sometimes,
        PreferNotToSay
    }

    public enum EducationLevelType
    {
        SecondarySchool,
        Undergrad,
        Postgrad,
        PreferNotToSay
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

    public enum LanguageType
    {
        English,
        Hindi,

        // 🏔️ Ladakh (UT)
        Ladakhi,
        //Balti,
        //Shina,
        //Urdu,

        // 🏔️ Himachal Pradesh
        Kinnauri,
        Lahauli,
        SpitiBhoti,
        Tibetan,
        //PahariDialects,

        // 🏔️ Uttarakhand
        //Jad,
        //Byangsi,
        //Chaudangsi,
        //Rangkas,

        // 🏔️ Sikkim
        Bhutia,
        Lepcha,
        Nepali,

        // 🌿 Arunachal Pradesh
        //Adi,
        //Apatani,
        Nyishi,
        //Mishmi,
        //Tagin,
        Monpa,
        //Sherdukpen,
        //Nocte,
        //Wancho,

        // 🌿 Nagaland
        //Ao,
        //Sema,
        //Lotha,
        //Angami,
        //Chakhesang,
        //Konyak,
        //Phom,
        //Chang,
        //Nagamese,

        // 🌿 Manipur
        //Meiteilon,
        //Tangkhul,
        //Mao,
        //Paite,
        //Thadou,
        //Hmar,
        //Zou,
        //Kuki,
        //Chin,

        // 🌿 Mizoram
        //Mizo,
        //Lai,
        //Mara,

        // 🌿 Assam (Hill Tribes)
        //Bodo,
        //Dimasa,
        //Karbi,
        //Tiwa,
        //Mising,
        //Rabha,

        // 🏔️ Nepal
        //Tamang,
        //Gurung,
        //Sherpa,
        //Limbu,
        //Rai,
        //Magar,
        //Thakali,
        //Newar,

        // 🏔️ Bhutan
        Dzongkha,
        //Sharchhopkha,
        //LhotshampaDialects,
        //Brokpa,

        // Common languages
        //Assamese,
        //Bengali,
        //Sanskrit,
        Other
    }
}