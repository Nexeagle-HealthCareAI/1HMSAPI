using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteAssetHandler : IRequestHandler<DeleteAssetRequestModel, DeleteAssetResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly AppDbContext _context;
        private readonly string _containerName;

        public DeleteAssetHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<DeleteAssetResponseModel> Handle(DeleteAssetRequestModel request, CancellationToken cancellationToken)
        {
            var response = new DeleteAssetResponseModel();
            bool blobDeleted = false;

            if (!string.IsNullOrEmpty(request.BlobAssetId))
            {
                blobDeleted = await _blobStorageService.DeleteAsync(request.BlobAssetId, _containerName, cancellationToken);
            }

            if (blobDeleted)
            {
                var asset = await _context.PrescriptionAssets
                    .FirstOrDefaultAsync(a => a.PrescriptionAssetId == request.PrescriptionAssestId, cancellationToken);

                if (asset != null)
                {
                    _context.PrescriptionAssets.Remove(asset);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                response.Success = true;
                response.Message = "Asset deleted successfully.";
            }
            else
            {
                response.Success = false;
                response.Message = "Failed to delete asset from blob storage.";
            }

            return response;
        }
    }
}
