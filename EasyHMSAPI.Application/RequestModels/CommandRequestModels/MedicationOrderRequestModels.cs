using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Places a medication order (one or more drugs) against an admission. Each line with a
    // ChargeId gets its own BillingChargeEvent posted immediately (charge-on-event), reusing the
    // existing AddChargeEvent engine for GST/discount/incentive — this handler doesn't duplicate
    // that math. Fails the whole order if any chargeable line can't be billed (e.g. encounter
    // closed), rather than silently leaving an order unbilled.
    [ExcludeFromCodeCoverage]
    public class PlaceMedicationOrderRequestModel : IRequest<PlaceMedicationOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public Guid? OrderedByDoctorId { get; set; }
        public string? Notes { get; set; }

        public List<MedicationOrderLineInput> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MedicationOrderLineInput
    {
        // ChargeMaster pharmacy item this line bills against — omit for a drug with no billing link.
        public Guid? ChargeId { get; set; }
        public string DrugName { get; set; } = null!;
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }
        public decimal Qty { get; set; } = 1;
    }

    // Discontinues one line of a medication order (stop this drug, keep the rest of the order
    // running). Voids the line's charge event too, if one was posted — a discontinued drug
    // shouldn't stay billed.
    [ExcludeFromCodeCoverage]
    public class DiscontinueMedicationOrderLineRequestModel : IRequest<DiscontinueMedicationOrderLineResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid OrderLineId { get; set; }
        public string? Reason { get; set; }
    }
}
