using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class FinalizeBillingRequestModel : IRequest<FinalizeBillingResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public string? Type { get; set; }
        public string? Reason { get; set; }
        public string? LoggedInUserName { get; set; }
    }
}
