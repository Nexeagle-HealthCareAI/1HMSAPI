using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UploadProfilePictureRequestModel : IRequest<UploadProfilePictureResponseModel>
    {
        public IFormFile? File { get; set; }
        public Guid UserId { get; set; }
    }
}
