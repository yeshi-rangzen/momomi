namespace MomomiAPI.Models.DTOs
{
    public class DiscoverySettingsDTO
    {
        public bool EnableGlobalDiscovery { get; set; }
        public bool IsDiscoverable { get; set; }
        public bool IsGloballyDiscoverable { get; set; }
        public int MaxDistanceKm { get; set; }
        public int MinAge { get; set; }
        public int MaxAge { get; set; }
        public bool HasLocation { get; set; }
    }
}
