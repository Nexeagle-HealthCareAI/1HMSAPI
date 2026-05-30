using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDraftInvoiceRequestModel : IRequest<CreateDraftInvoiceResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public decimal? InvoiceDiscountAmount { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
