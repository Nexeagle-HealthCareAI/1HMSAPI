using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UnsignDischargeSummaryRequestModel : IRequest<UnsignDischargeSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? LoggedInUserName { get; set; }
    }
}