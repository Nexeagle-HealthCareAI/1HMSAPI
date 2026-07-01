using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMarGridResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime DayStartUtc { get; set; }
        public DateTime DayEndUtc { get; set; }
        public List<MarLineItem> Lines { get; set; } = new();
    }

    // One MEDICATION order line (ACTIVE, or discontinued after the viewed day started) with every
    // computed slot for this day folded in.
    [ExcludeFromCodeCoverage]
    public class MarLineItem
    {
        public Guid OrderLineId { get; set; }
        public Guid OrderId { get; set; }
        public string? ItemName { get; set; }
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public string? Instructions { get; set; }
        public bool IsHighAlert { get; set; }
        public string OrderLineStatusCode { get; set; } = null!;   // ACTIVE / DISCONTINUED
        public bool IsAdHocOnly { get; set; }   // true for SOS/PRN and unrecognized/legacy free-text Frequency

        public List<MarSlotItem> Slots { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MarSlotItem
    {
        public DateTime ScheduledForUtc { get; set; }
        public string Status { get; set; } = null!;   // PENDING/DUE/OVERDUE/MISSED/ADMINISTERED/HELD/REFUSED/PATIENT_NOT_AVAILABLE

        // Populated only when Status reflects an actual recorded administration.
        public Guid? MedicationAdministrationId { get; set; }
        public DateTime? ActedAt { get; set; }
        public string? ActedBy { get; set; }
        public string? AdministeredDose { get; set; }
        public string? AdministeredRoute { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public bool WitnessRequired { get; set; }
        public string? WitnessName { get; set; }
        public DateTime? WitnessConfirmedAt { get; set; }
    }
}
