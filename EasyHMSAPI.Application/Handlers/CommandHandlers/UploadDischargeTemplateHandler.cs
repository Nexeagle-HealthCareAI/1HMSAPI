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
    /// <summary>Mirrors UploadPrescriptionTemplateHandler.</summary>
    public class UploadDischargeTemplateHandler : IRequestHandler<UploadDischargeTemplateRequestModel, UploadDischargeTemplateResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadDischargeTemplateHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:DischargeTemplatesContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<UploadDischargeTemplateResponseModel> Handle(UploadDischargeTemplateRequestModel request, CancellationToken cancellationToken)
        {
            UploadDischargeTemplateResponseModel result = new();
            try
            {
                var existingDoctor = await _context.Doctors
                   .AsNoTracking()
                   .FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (existingDoctor == null)
                {
                    result.Success = false;
                    result.Message = "Invalid doctor Id";
                    return result;
                }

                var existingHospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (existingHospital == null)
                {
                    result.Success = false;
                    result.Message = "Invalid hospital Id";
                    return result;
                }

                var dischargeSettings = await _context.DischargeSettings
                    .Where(x => x.DoctorId == request.DoctorId && x.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                var newDischargeSettingId = Guid.NewGuid();
                var currentDateTime = DateTime.UtcNow;

                if (dischargeSettings == null)
                {
                    DischargeSetting newDischargeSetting = new()
                    {
                        DischargeSettingId = newDischargeSettingId,
                        HospitalId = request.HospitalId,
                        DoctorId = request.DoctorId,
                        CreatedAt = currentDateTime,
                        UpdatedAt = currentDateTime,
                        CreatedByUserId = request.LoggedInUserId
                    };
                    _context.DischargeSettings.Add(newDischargeSetting);
                    await _context.SaveChangesAsync(cancellationToken);

                    dischargeSettings = newDischargeSetting;
                }

                var url = await _blobStorageService.UploadAsync(request.DoctorId.ToString() + "_" + request.HospitalId.ToString(), request.File, _containerName, cancellationToken);

                dischargeSettings.URI = url;
                dischargeSettings.UpdatedAt = currentDateTime;

                await _context.SaveChangesAsync(cancellationToken);

                result.Success = true;
                result.Url = url;
                result.Message = "Discharge letterhead template uploaded successfully.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Url = null;
                result.Message = $"An error occurred while uploading the discharge letterhead template: {ex.Message}";
            }

            return result;
        }
    }
}
