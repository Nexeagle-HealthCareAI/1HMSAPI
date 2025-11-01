using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeleteAssetRequestModel : IRequest<DeleteAssetResponseModel>
    {
        public Guid PrescriptionAssestId { get; set; }
        public string? BlobAssetId { get; set; }
    }
}
