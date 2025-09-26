using MomomiAPI.Models.Enums;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MomomiAPI.Models.Entities
{
    [Table("users")]
    public class User
    {
        // ========================================
        // CORE IDENTITY & AUTHENTICATION
        // Primary keys and authentication identifiers
        // ========================================

        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("supabase_uid")]
        [Required]
        public Guid SupabaseUid { get; set; }

        [Column("email")]
        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        // ========================================
        // PERSONAL INFORMATION
        // Basic demographic and identity information
        // ========================================

        [Column("first_name")]
        [MaxLength(100)]
        public required string FirstName { get; set; }

        [Column("last_name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [Column("date_of_birth")]
        public DateTime DateOfBirth { get; set; }

        [Column("gender")]
        public GenderType Gender { get; set; }

        [Column("interested_in")]
        public GenderType InterestedIn { get; set; }

        [Column("height_cm")]
        public int HeightCm { get; set; }

        // ========================================
        // LOCATION & GEOGRAPHY
        // Geographic location and area information
        // ========================================
        [Column("location", TypeName = "geography (point, 4326)")]
        public required Point Location { get; set; }

        [Column("latitude")]
        public double Latitude { get; set; }

        [Column("longitude")]
        public double Longitude { get; set; }

        [Column("hometown")]
        [MaxLength(200)]
        public string Hometown { get; set; } = string.Empty;

        [Column("neighbourhood")]
        [MaxLength(200)]
        public string? Neighbourhood { get; set; }

        // ========================================
        // PROFILE & BIO
        // User's self-description and profile content
        // ========================================

        [Column("bio")]
        public string? Bio { get; set; }

        // ========================================
        // CULTURAL & BACKGROUND INFORMATION
        // Heritage, religion, languages, and cultural identity
        // ========================================

        [Column("heritage")]
        public List<HeritageType> Heritage { get; set; } = [];

        [Column("religion")]
        public List<ReligionType> Religion { get; set; } = [];

        [Column("languages_spoken")]
        public List<LanguageType> LanguagesSpoken { get; set; } = [];

        // ========================================
        // EDUCATION & CAREER
        // Professional and educational background
        // ========================================

        [Column("education_level")]
        [MaxLength(100)]
        public EducationLevelType? EducationLevel { get; set; }

        [Column("occupation")]
        [MaxLength(100)]
        public string? Occupation { get; set; }

        // ========================================
        // LIFESTYLE & FAMILY
        // Family status, children, and future plans
        // ========================================

        [Column("children")]
        public ChildrenStatusType? Children { get; set; }

        [Column("family_plan")]
        public FamilyPlanType? FamilyPlan { get; set; }

        // ========================================
        // LIFESTYLE HABITS & VICES
        // Substance use and lifestyle choices
        // ========================================

        [Column("drugs")]
        public ViceFrequencyType? Drugs { get; set; }

        [Column("smoking")]
        public ViceFrequencyType? Smoking { get; set; }

        [Column("drinking")]
        public ViceFrequencyType? Drinking { get; set; }

        [Column("marijuana")]
        public ViceFrequencyType? Marijuana { get; set; }

        // ========================================
        // MATCHING & DISCOVERY PREFERENCES
        // User preferences for finding matches
        // ========================================

        [Column("max_distance_km")]
        public int MaxDistanceKm { get; set; } = 50;

        [Column("min_age")]
        public int MinAge { get; set; } = 18;

        [Column("max_age")]
        public int MaxAge { get; set; } = 35;

        [Column("enable_global_discovery")]
        public bool EnableGlobalDiscovery { get; set; } = true;

        [Column("is_discoverable")]
        public bool IsDiscoverable { get; set; } = true;

        [Column("is_globally_discoverable")]
        public bool IsGloballyDiscoverable { get; set; } = true;

        // ========================================
        // NOTIFICATIONS & DEVICE
        // Push notifications and device tokens
        // ========================================

        [Column("push_token")]
        [MaxLength(500)]
        public string? PushToken { get; set; }

        [Column("notifications_enabled")]
        public bool NotificationsEnabled { get; set; } = true;

        // ========================================
        // ACCOUNT STATUS & VERIFICATION
        // Account state and verification flags
        // ========================================

        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_onboarding")]
        public bool IsOnboarding { get; set; } = true;

        // ========================================
        // TIMESTAMPS & AUDIT
        // Creation, update, and activity tracking
        // ========================================

        [Column("last_active")]
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ========================================
        // NAVIGATION PROPERTIES
        // Entity Framework relationships and collections
        // ========================================

        // User content and media
        public virtual ICollection<UserPhoto> Photos { get; set; } = [];

        // Matching and interactions
        public virtual ICollection<UserSwipe> SwipesGiven { get; set; } = [];
        public virtual ICollection<UserSwipe> SwipesReceived { get; set; } = [];

        // Conversations and messaging
        public virtual ICollection<Conversation> ConversationsAsUser1 { get; set; } = [];
        public virtual ICollection<Conversation> ConversationsAsUser2 { get; set; } = [];
        public virtual ICollection<Message> MessagesSent { get; set; } = [];

        // User preferences and settings
        public virtual UserPreference? Preferences { get; set; }

        // Subscription and usage tracking
        public virtual UserSubscription? Subscription { get; set; }
        public virtual UserUsageLimit? UsageLimit { get; set; }

        // Notifications
        public virtual ICollection<PushNotification> Notifications { get; set; } = [];
    }
}