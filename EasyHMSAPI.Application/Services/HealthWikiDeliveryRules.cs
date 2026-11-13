using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>How a WhatsApp status callback changes the delivery state recorded for a Health Wiki message.</summary>
    public static class HealthWikiDeliveryRules
    {
        /// <summary>WhatsApp's status word as ours: DELIVERED, READ or FAILED. "sent" (and anything unknown) says nothing new, so null.</summary>
        public static string? Normalize(string? status) => status?.Trim().ToLowerInvariant() switch
        {
            "delivered" => HealthWikiNotification.DeliveryDelivered,
            "read" => HealthWikiNotification.DeliveryRead,
            "failed" => HealthWikiNotification.DeliveryFailed,
            _ => null,
        };

        private static int Rank(string? s) => s switch
        {
            HealthWikiNotification.DeliveryDelivered => 1,
            HealthWikiNotification.DeliveryRead => 2,
            _ => 0,
        };

        /// <summary>Callbacks can arrive out of order or twice. A message that was delivered or read never goes back to nothing or to
        /// failed, and delivered never replaces read; a failure is recorded only while nothing better is known.</summary>
        public static bool ShouldApply(string? current, string incoming)
        {
            if (incoming == HealthWikiNotification.DeliveryFailed) return current == null;
            return Rank(incoming) > Rank(current) || current == HealthWikiNotification.DeliveryFailed;
        }

        /// <summary>WhatsApp's own reason on a failure, cut to what the column holds.</summary>
        public static string? Detail(string? detail)
        {
            var d = detail?.Trim();
            if (string.IsNullOrEmpty(d)) return null;
            return d.Length <= 300 ? d : d[..300];
        }
    }
}
