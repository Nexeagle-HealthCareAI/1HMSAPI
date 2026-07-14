using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetRapidResponseHistoryRequestModel : IRequest<GetRapidResponseHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }

    // Hospital-wide open activations — drives the ICU board's "RRT active" flag and a future
    // Rapid Response mini-board, without every consumer re-deriving "open" from raw history rows.
    [ExcludeFromCodeCoverage]
    public class GetOpenRapidResponsesRequestModel : IRequest<GetOpenRapidResponsesResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
