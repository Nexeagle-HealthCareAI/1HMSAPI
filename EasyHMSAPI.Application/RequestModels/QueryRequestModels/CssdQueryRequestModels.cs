using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetInstrumentSetsRequestModel : IRequest<GetInstrumentSetsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSterilizationCycleHistoryRequestModel : IRequest<GetSterilizationCycleHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Take { get; set; } = 50;
    }
}
