
namespace MomomiAPI.Helpers
{
    public class LocationHelper
    {
        // Earths radius in kilometers
        private const double EarthRadiusKm = 6371.0;

        // Earth's radius in miles
        private const double EarthRadiusMiles = 3958.756;

        /// <summary>
        /// Checks if two geographic points are within the specified distance
        /// </summary>
        /// <returns>True if points are within the specified distance</returns> 
        public static bool IsWithinDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2, int maxDistanceKm)
        {
            // Validate coordinates
            if (!AreValidCoordinates(lat1, lon1) || !AreValidCoordinates(lat2, lon2))
            {
                return false;
            }

            // Quick distance check - if the difference in coordinates is too large, skip expensive calculation
            if (Math.Abs(lat1 - lat2) > (decimal)(maxDistanceKm / 111.0) || // ~111km per degree of latitude
                Math.Abs(lon1 - lon2) > (decimal)(maxDistanceKm / 111.0))   // Rough approximation
            {
                return false;
            }

            var distance = CalculateDistance(lat1, lon1, lat2, lon2);
            return distance <= maxDistanceKm;
        }

        /// <summary>
        /// Calculates the distance between two geographic points using the Haversine formula
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        public static double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            // Validate coordinates
            if (!AreValidCoordinates(lat1, lon1) || !AreValidCoordinates(lat2, lon2))
            {
                throw new ArgumentException("Invalid coordinates provided");
            }

            // Convert decimal to double for mathematical operations
            double dLat1 = (double)lat1;
            double dLon1 = (double)lon1;
            double dLat2 = (double)lat2;
            double dLon2 = (double)lon2;

            return CalculateHaversineDistance(dLat1, dLon1, dLat2, dLon2, EarthRadiusKm);
        }

        /// <summary>
        /// Implements the Haversine formula for calculating great-circle distances
        /// </summary>
        /// <param name="earthRadius">Earth's radius in desired unit (km or miles)</param>
        /// <returns>Distance in the same unit as earthRadius</returns>
        private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2, double earthRadius)
        {
            // Convert degrees to radians
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double radLat1 = DegreesToRadians(lat1);
            double radLat2 = DegreesToRadians(lat2);

            // Haversine formula
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                       Math.Cos(radLat1) * Math.Cos(radLat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }

        /// <summary>
        /// Validates if the provided coordinates are within valid ranges
        /// </summary>
        /// <returns>True if coordinates are valid</returns>
        private static bool AreValidCoordinates(decimal latitude, decimal longitude)
        {
            return latitude >= -90 && latitude <= 90 &&
                   longitude >= -180 && longitude <= 180 &&
                   latitude != 0 && longitude != 0; // Exclude default/unset coordinates
        }

        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        /// <returns>Angle in radians</returns>
        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
