using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HealthWikiNotificationInfo
    {
        public string Code { get; set; } = null!;
        public string TemplateName { get; set; } = null!;
        public string? RecipientMobileMasked { get; set; }
        public string EntityType { get; set; } = null!;
        public Guid EntityId { get; set; }
        // SENT (WhatsApp accepted it) | FAILED | SKIPPED
        public string Status { get; set; } = null!;
        public string? Detail { get; set; }
        // After acceptance: DELIVERED | READ | FAILED, or null while WhatsApp has not said.
        public string? DeliveryStatus { get; set; }
        public string? DeliveryDetail { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveryAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiNotificationsResponseModel
    {
        public bool Success { get; set; } = true;
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public List<HealthWikiNotificationInfo> Notifications { get; set; } = new();
    }
}
