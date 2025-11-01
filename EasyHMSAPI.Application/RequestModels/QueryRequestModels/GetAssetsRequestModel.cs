using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetAssetsRequestModel : IRequest<GetAssetsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
