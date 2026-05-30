using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingEventsRequestModel : IRequest<GetBillingEventsResponseModel>
    {
        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? InvoiceId { get; set; }
    }
}
