using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetEarlyWarningAutoFillRequestModel : IRequest<GetEarlyWarningAutoFillResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetEarlyWarningScoreHistoryRequestModel : IRequest<GetEarlyWarningScoreHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
