using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadProfilePictureRequestModel : IRequest<UploadProfilePictureResponseModel>
    {
        public IFormFile? File { get; set; }
        public Guid UserId { get; set; }
    }
}
