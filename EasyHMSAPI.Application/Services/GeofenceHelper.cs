namespace EasyHMSAPI.Application.Services
{
    // Great-circle distance check for the OPD QR check-in flow -- mirrors the WhatsApp gateway's
    // own geo.py:haversine_km() math, reimplemented server-side since the server must be the
    // authoritative check (a client-reported "I'm within range" can't be trusted).
    public static class GeofenceHelper
    {
        private const double EarthRadiusMeters = 6371000;

        public static double DistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            var phi1 = ToRadians((double)lat1);
            var phi2 = ToRadians((double)lat2);
            var deltaPhi = ToRadians((double)(lat2 - lat1));
            var deltaLambda = ToRadians((double)(lon2 - lon1));

            var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2)
                  + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusMeters * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
