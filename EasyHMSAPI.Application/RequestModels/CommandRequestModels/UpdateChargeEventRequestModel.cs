using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Corrects an already-posted charge line (qty/rate/discount/name) in place — a "fix a mistake"
    // action, distinct from delete+re-add. No admin approval / discount-cap gate: this and every
    // other billing money-safety gate were removed per product decision (see UpdateChargeEventHandler).
    [ExcludeFromCodeCoverage]
    public class UpdateChargeEventRequestModel : IRequest<UpdateChargeEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid ChargeEventId { get; set; }

        public string? DisplayName { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscountPercent { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
