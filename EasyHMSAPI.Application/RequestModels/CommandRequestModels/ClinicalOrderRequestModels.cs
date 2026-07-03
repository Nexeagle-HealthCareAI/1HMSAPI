using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Places a CPOE order (one or more lines) against an admission. Each line with a ChargeId
    // gets its own BillingChargeEvent posted immediately (charge-on-event), reusing the existing
    // AddChargeEvent engine for GST/discount/incentive — this handler doesn't duplicate that math.
    // Fails the whole order if any chargeable line can't be billed (e.g. encounter closed), rather
    // than silently leaving an order unbilled. One generic shape for every OrderType (see
    // IpdConstants.ClinicalOrderType) — Dose/Route/Frequency/DurationDays are MEDICATION-only,
    // Urgency/ScheduledAt are mainly LAB/RADIOLOGY/PROCEDURE, all null/unused otherwise.
    [ExcludeFromCodeCoverage]
    public class PlaceClinicalOrderRequestModel : IRequest<PlaceClinicalOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string OrderType { get; set; } = null!;
        public Guid? OrderedByDoctorId { get; set; }
        public string? Notes { get; set; }

        public List<ClinicalOrderLineInput> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ClinicalOrderLineInput
    {
        // ChargeMaster item this line bills against — omit for an item with no billing link.
        public Guid? ChargeId { get; set; }
        public string ItemName { get; set; } = null!;

        // Medication-only.
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }

        // Mainly Lab/Radiology/Procedure.
        public string? Urgency { get; set; }
        public DateTime? ScheduledAt { get; set; }

        // Medication-only: requires a second-nurse witness co-sign at MAR administration.
        public bool IsHighAlert { get; set; }

        public decimal Qty { get; set; } = 1;
    }

    // Discontinues one line of a CPOE order (stop this item, keep the rest of the order running).
    // Voids the line's charge event too, if one was posted — a discontinued item shouldn't stay
    // billed. Type-agnostic: works the same for any OrderType.
    [ExcludeFromCodeCoverage]
    public class DiscontinueClinicalOrderLineRequestModel : IRequest<DiscontinueClinicalOrderLineResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid OrderLineId { get; set; }
        public string? Reason { get; set; }
    }
}
