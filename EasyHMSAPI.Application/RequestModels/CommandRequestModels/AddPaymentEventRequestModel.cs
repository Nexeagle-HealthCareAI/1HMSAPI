using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AddPaymentEventRequestModel : IRequest<AddPaymentEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public PaymentDetail? Payment { get; set; }
        public List<ExtraChargeDetail>? ExtraCharges { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ExtraChargeDetail
    {
        public string? Reason { get; set; }
        public decimal Amount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PaymentDetail
    {
        public string? PaymentType { get; set; }
        public string? PaymentMode { get; set; }
        public string? Description { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
    }
}
