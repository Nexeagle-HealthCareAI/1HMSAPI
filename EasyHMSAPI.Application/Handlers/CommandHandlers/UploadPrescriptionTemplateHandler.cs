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
    public class UploadPrescriptionTemplateHandler : IRequestHandler<UploadPrescriptionTemplateRequestModel, UploadPrescriptionTemplateResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadPrescriptionTemplateHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PrescriptionTemplatesContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<UploadPrescriptionTemplateResponseModel> Handle(UploadPrescriptionTemplateRequestModel request, CancellationToken cancellationToken)
        {
            UploadPrescriptionTemplateResponseModel result = new();
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

                var prescriptionSettings = await _context.PrescriptionSettings
                .Where(x => x.DoctorId == request.DoctorId && x.HospitalId == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
                var newPresciptionSettingId = Guid.NewGuid();
                var currentDateTime = DateTime.UtcNow;

                if (prescriptionSettings == null)
                {
                    PrescriptionSetting newPrescriptionSetting = new()
                    {
                        PrescriptionSettingId = newPresciptionSettingId,
                        HospitalId = request.HospitalId,
                        DoctorId = request.DoctorId,
                        CreatedAt = currentDateTime,
                        UpdatedAt = currentDateTime,
                        CreatedByUserId = request.LoggedInUserId
                    };
                    _context.PrescriptionSettings.Add(newPrescriptionSetting);
                    await _context.SaveChangesAsync(cancellationToken);

                    prescriptionSettings = newPrescriptionSetting;
                }

                var url = await _blobStorageService.UploadAsync(request.DoctorId.ToString() + "_" + request.HospitalId.ToString(), request.File, _containerName, cancellationToken);

                prescriptionSettings.URI = url;
                prescriptionSettings.UpdatedAt = currentDateTime;

                await _context.SaveChangesAsync(cancellationToken);

                result.Success = true;
                result.Url = url;
                result.Message = "Prescription template uploaded successfully.";
            }
            catch(Exception ex)
            {
                result.Success = false;
                result.Url = null;
                result.Message = $"An error occurred while uploading the prescription template: {ex.Message}";
            }

            return result;
        }
    }
}
