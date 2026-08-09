using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // NurseUserId defaults to the caller (resolved by the controller from the auth token) --
    // WardCode/ShiftCode are optional further filters on top of the nurse's own roster.
    [ExcludeFromCodeCoverage]
    public class GetNursingStationSummaryRequestModel : IRequest<GetNursingStationSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? NurseUserId { get; set; }
        public string? WardCode { get; set; }
        public string? ShiftCode { get; set; }
    }
}
