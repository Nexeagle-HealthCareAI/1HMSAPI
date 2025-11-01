using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAssetsHandler : IRequestHandler<GetAssetsRequestModel, GetAssetsResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public GetAssetsHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<GetAssetsResponseModel> Handle(GetAssetsRequestModel request, CancellationToken cancellationToken)
        {
            var doctorExists = await _context.Doctors.Where(x => x.DoctorID == request.DoctorId).Select(x => x.DoctorID).FirstOrDefaultAsync(cancellationToken);
            var assets = new List<AssetsDataModel>();
            GetAssetsResponseModel result = new();

            if (doctorExists == Guid.Empty)
            {
                result.DoctorId = request.DoctorId;
                result.Assets = assets;
                result.Success = false;
                result.Message = "Doctor not found.";
                return result;
            }
            else
            {
                var assetEntities = await _context.PrescriptionAssets
                    .Where(a => a.DoctorId == request.DoctorId)
                    .ToListAsync(cancellationToken);

                foreach (var asset in assetEntities)
                {
                    var blobUrl = asset.BlobUrl;
                    var blobName = blobUrl;
                    var questionMarkIndex = blobUrl.IndexOf('?');
                    if (questionMarkIndex > 0)
                        blobName = blobUrl.Substring(0, questionMarkIndex);

                    blobName = Path.GetFileName(blobName);

                    assets.Add(new AssetsDataModel
                    {
                        BlobAssetId = blobName,
                        PrescriptionAssestId = asset.PrescriptionAssetId,
                        AssetType = asset.AssetType,
                        BlobUrl = asset.BlobUrl
                    });
                }

                result.DoctorId = request.DoctorId;
                result.Assets = assets;
                result.Success = assets.Count > 0;
                result.Message = assets.Count > 0 ? "Assets retrieved successfully." : "No assets found.";
            }

            return result;
        }
    }
}
