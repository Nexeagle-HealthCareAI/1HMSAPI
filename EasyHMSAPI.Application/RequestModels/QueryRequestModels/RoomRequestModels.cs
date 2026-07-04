using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetRoomsRequestModel : IRequest<GetRoomsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    [ExcludeFromCodeCoverage]
    public class GetRoomByIdRequestModel : IRequest<RoomDetailResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid RoomId { get; set; }
    }
}
