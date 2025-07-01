
namespace MomomiAPI.Helpers
{
    public class LocationHelper
    {
        /// <summary>
        /// Calculate distance between two coordinates using Haversine formula
        /// </summary>
        /// <param name="lat1">Latitude of first point</param>
        /// <param name="lon1">Longitude of first point</param>
        /// <param name="lat2">Latitude of second point</param>
        /// <param name="lon2">Longitude of second point</param>
        /// <returns>Distance in kilometers</returns>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula to calculate distance between two coordinates
            const double earthRadiusKm = 6371.0;

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c; // Distance in kilometers
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Check if coordinates are within a specific radius
        /// </summary>
        public static bool IsWithinRadius(double lat1, double lon1, double lat2, double lon2, double radiusKm)
        {
            return CalculateDistance(lat1, lon1, lat2, lon2) <= radiusKm;
        }

        /// <summary>
        /// Get approximate city/region from coordinates (simplified version)
        /// In production, use a proper geocoding service
        /// </summary>
        public static string GetApproximateLocation(double latitude, double longitude)
        {
            // Simplified regional mapping for Himalayan/Northeast regions
            var regions = new Dictionary<(double, double, double, double), string>
            {
                // Format: (minLat, maxLat, minLon, maxLon), "Region Name"
                { (26.0, 30.0, 86.0, 89.0), "Sikkim/Darjeeling" },
                { (27.0, 29.0, 88.0, 92.0), "Bhutan" },
                { (32.0, 35.0, 76.0, 79.0), "Ladakh" },
                { (27.0, 29.0, 84.0, 89.0), "Nepal" },
                { (29.0, 32.0, 78.0, 82.0), "Uttarakhand" },
                { (24.0, 28.0, 91.0, 98.0), "Northeast India" },
                { (26.0, 28.0, 88.0, 95.0), "Assam" },
                { (23.0, 25.0, 92.0, 94.0), "Tripura" },
                { (24.0, 26.0, 92.0, 94.0), "Mizoram" },
                { (24.0, 26.0, 93.0, 95.0), "Manipur" },
                { (25.0, 28.0, 93.0, 97.0), "Nagaland" },
                { (25.0, 27.0, 89.0, 92.0), "Meghalaya" },
                { (26.0, 29.0, 91.0, 95.0), "Arunachal Pradesh" }
            };

            foreach (var region in regions)
            {
                var bounds = region.Key;
                if (latitude >= bounds.Item1 && latitude <= bounds.Item2 &&
                    longitude >= bounds.Item3 && longitude <= bounds.Item4)
                {
                    return region.Value;
                }
            }

            return "Unknown Region";
        }
    }
}
