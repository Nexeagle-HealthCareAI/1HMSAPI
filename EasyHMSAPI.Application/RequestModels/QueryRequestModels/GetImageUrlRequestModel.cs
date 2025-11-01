using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetImageUrlRequestModel : IRequest<GetImageUrlResponseModel>
    {
        public string? FileName { get; set; }
        public string? ContainerName { get; set; }
    }
}