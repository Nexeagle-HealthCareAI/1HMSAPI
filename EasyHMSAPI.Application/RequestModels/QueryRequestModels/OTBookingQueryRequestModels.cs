using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetOperationTheatresRequestModel : IRequest<GetOperationTheatresResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetOTScheduleRequestModel : IRequest<GetOTScheduleResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}
