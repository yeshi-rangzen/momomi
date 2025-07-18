namespace MomomiAPI.Models
{
    public class RegisterVerificationData
    {
        public string Email { get; set; }
        public string SupabaseUserId { get; set; }
        public DateTime VerifiedAt { get; set; }

        public RegisterVerificationData(string email, string supabaseUserId, DateTime verifiedAt)
        {
            Email = email;
            SupabaseUserId = supabaseUserId;
            VerifiedAt = verifiedAt;
        }
    }
}
