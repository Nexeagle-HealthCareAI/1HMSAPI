using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetClinicalOrdersResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ClinicalOrderItem> Orders { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ClinicalOrderItem
    {
        public Guid OrderId { get; set; }
        public string StatusCode { get; set; } = null!;
        public DateTime OrderedAt { get; set; }
        public string? OrderedBy { get; set; }
        public string? Notes { get; set; }
        public List<ClinicalOrderLineItem> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ClinicalOrderLineItem
    {
        public Guid OrderLineId { get; set; }
        public string? ItemName { get; set; }
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }
        public string? Urgency { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool IsHighAlert { get; set; }
        public decimal Qty { get; set; }
        public string StatusCode { get; set; } = null!;

        // Billing side, if this line was chargeable.
        public Guid? ChargeEventId { get; set; }
        public decimal? ChargedAmount { get; set; }
        public bool ChargeVoided { get; set; }
    }
}
