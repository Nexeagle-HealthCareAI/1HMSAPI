using MediatR;
using Microsoft.AspNetCore.Http;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadImageRequestModel : IRequest<UploadImageResponseModel>
    {
        public IFormFile? File { get; set; }
        public string? FileName { get; set; }
        public string? ContainerName { get; set; }
    }
}