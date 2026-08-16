using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AddChargeEventRequestModel : IRequest<AddChargeEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public List<ChargeDetail>? Charges { get; set; }

        // Optional billing-recipient context for GST. If null, falls back to supplier policy.
        public string? PlaceOfSupplyStateCode { get; set; }
        public string? BuyerGstin { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        // Populated from the Idempotency-Key request header (offline outbox replay), not the body.
        [JsonIgnore]
        public string? IdempotencyKey { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ChargeDetail
    {
        // Optional link to ChargeMaster — when set, HSN/GST/incentive snapshot is taken from there.
        public Guid? ChargeId { get; set; }

        public string? DisplayName { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscountPercent { get; set; }
        public string? CategoryCode { get; set; }
        
        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        // GST overrides — when supplied they override the ChargeMaster snapshot.
        public string? HsnSacCode { get; set; }
        public decimal? GstRate { get; set; }
        public bool? TaxInclusive { get; set; }

        // Incentive override for this line. If null, seeded from ChargeMaster.IncentiveAmount.
        public decimal? IncentiveAmount { get; set; }

        // Best-effort treating-doctor attribution, resolved by the caller (CPOE: the order's
        // OrderedByDoctorId; OT: the surgery case's SurgeonDoctorId). Drives consultant incentive
        // ledger accrual when set alongside a positive IncentiveAmount.
        public Guid? AttributedDoctorId { get; set; }

        // Optional reason captured when the discount exceeds policy / charge cap.
        public string? DiscountReason { get; set; }
    }
}
