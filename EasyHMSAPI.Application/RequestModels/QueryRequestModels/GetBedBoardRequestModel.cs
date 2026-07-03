using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBedBoardRequestModel : IRequest<GetBedBoardResponseModel>
    {
        public Guid HospitalId { get; set; }
        // Optional ward filter; omitted returns every ward (client can group by WardCode).
        public string? WardCode { get; set; }
    }
}
