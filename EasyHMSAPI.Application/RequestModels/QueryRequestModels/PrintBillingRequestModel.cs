using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PrintBillingRequestModel : IRequest<PrintBillingResponseModel>
    {
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid EncounterId { get; set; }
    }
}
