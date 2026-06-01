using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CloseAdmissionDayRequestModel : IRequest<CloseAdmissionDayResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReopenAdmissionDayRequestModel : IRequest<ReopenAdmissionDayResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionDayBillId { get; set; }
        public string? Reason { get; set; }
        public string? LoggedInUserName { get; set; }
    }
}
