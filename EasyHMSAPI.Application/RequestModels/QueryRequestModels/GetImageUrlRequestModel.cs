using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetImageUrlRequestModel : IRequest<GetImageUrlResponseModel>
    {
        public string? FileName { get; set; }
        public string? ContainerName { get; set; }
    }
}