using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadAssetHandler : IRequestHandler<UploadAssetRequestModel, UploadAssetResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadAssetHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PrescriptionAssetsContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<UploadAssetResponseModel> Handle(UploadAssetRequestModel request, CancellationToken cancellationToken)
        {
            var doctorExists = await _context.Doctors.Where(x => x.DoctorID == request.DoctorId).Select(x => x.DoctorID).FirstOrDefaultAsync(cancellationToken);
            UploadAssetResponseModel result = new();

            if(doctorExists == Guid.Empty)
            {
                result.Success = false;
                result.AssestUrl = string.Empty;
                result.Message = "Doctor not found.";
            }
            else
            {
                var url = await _blobStorageService.UploadAsync(request.DoctorId, request.File, _containerName, cancellationToken);

                var asset = new PrescriptionAsset
                {
                    PrescriptionAssetId = Guid.NewGuid(),
                    DoctorId = request.DoctorId,
                    PrescriptionSettingId = request.PrescriptionSettingId,
                    AssetType = request.AssetType!,
                    BlobUrl = url,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.PrescriptionAssets.Add(asset);
                await _context.SaveChangesAsync(cancellationToken);

                result.Success = true;
                result.AssestUrl = url;
                result.Message = "Asset uploaded successfully.";
            }

            return result;
        }
    }
}
