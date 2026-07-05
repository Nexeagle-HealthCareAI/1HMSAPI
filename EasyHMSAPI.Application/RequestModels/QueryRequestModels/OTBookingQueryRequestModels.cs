using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetOperationTheatresRequestModel : IRequest<GetOperationTheatresResponseModel>
    {
        public Guid HospitalId { get; set; }
        // False (default) => only active theatres, for booking pickers. True => every theatre,
        // for the Configuration setup screen where inactive ones still need to be visible/editable.
        public bool IncludeInactive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetOTScheduleRequestModel : IRequest<GetOTScheduleResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    // Kanban plan board: every non-cancelled case, grouped client-side by StatusCode. Completed
    // cases only show up if completed today, so the board stays a "what's happening now" view
    // rather than an ever-growing history.
    [ExcludeFromCodeCoverage]
    public class GetOtBoardRequestModel : IRequest<GetOtBoardResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
