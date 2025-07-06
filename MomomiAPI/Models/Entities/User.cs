using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("users")]
    public class User
    {
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

        [Column("first_name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [Column("last_name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("gender")]
        public GenderType? Gender { get; set; }

        [Column("interested_in")]
        public GenderType? InterestedIn { get; set; }

        [Column("latitude", TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        [Column("longitude", TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("heritage")]
        public HeritageType? Heritage { get; set; }

        [Column("religion")]
        public ReligionType? Religion { get; set; }

        [Column("languages_spoken")]
        public List<string>? LanguagesSpoken { get; set; }

        [Column("education_level")]
        [MaxLength(100)]
        public string? EducationLevel { get; set; }

        [Column("occupation")]
        [MaxLength(100)]
        public string? Occupation { get; set; }

        [Column("height_cm")]
        public int? HeightCm { get; set; }

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

        [Column("push_token")]
        [MaxLength(500)]
        public string? PushToken { get; set; }

        [Column("notifications_enabled")]
        public bool NotificationsEnabled { get; set; } = true;

        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("last_active")]
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        // Navigation properties
        public virtual ICollection<UserPhoto> Photos { get; set; } = [];
        public virtual ICollection<UserLike> LikesGiven { get; set; } = [];
        public virtual ICollection<UserLike> LikesReceived { get; set; } = [];
        public virtual ICollection<Conversation> ConversationsAsUser1 { get; set; } = [];
        public virtual ICollection<Conversation> ConversationsAsUser2 { get; set; } = [];
        public virtual ICollection<Message> MessagesSent { get; set; } = [];
        public virtual ICollection<UserReport> ReportsMade { get; set; } = [];
        public virtual ICollection<UserReport> ReportsReceived { get; set; } = [];
        public virtual UserPreference? Preferences { get; set; }

        // Subscription and usage tracking
        public virtual UserSubscription? Subscription { get; set; }
        public virtual UserUsageLimit? UsageLimit { get; set; }

        // Blocking relationships
        public virtual ICollection<UserBlock> BlocksMade { get; set; } = [];
        public virtual ICollection<UserBlock> BlocksReceived { get; set; } = [];

        // Notifications
        public virtual ICollection<PushNotification> Notifications { get; set; } = [];
    }
}
