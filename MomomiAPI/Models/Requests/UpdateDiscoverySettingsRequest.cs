using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class UpdateDiscoverySettingsRequest
    {
        public bool? EnableGlobalDiscovery { get; set; }
        public bool? IsDiscoverable { get; set; }
        public bool? IsGloballyDiscoverable { get; set; }

        [Range(1, 200, ErrorMessage = "Max distance must be between 1 and 200 km")]
        public int? MaxDistanceKm { get; set; }

        [Range(18, 100, ErrorMessage = "Min age must be between 18 and 100")]
        public int? MinAge { get; set; }

        [Range(18, 100, ErrorMessage = "Max age must be between 18 and 100")]
        public int? MaxAge { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
