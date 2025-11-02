using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteAssetRequestModel : IRequest<DeleteAssetResponseModel>
    {
        public Guid PrescriptionAssestId { get; set; }
        public string? BlobAssetId { get; set; }
    }
}
