using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPrescriptionSettingsHandler : IRequestHandler<GetPrescriptionSettingsRequestModel, GetPrescriptionSettingsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _templatesContainer;

        public GetPrescriptionSettingsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _templatesContainer = configuration["BlobStorage:PrescriptionTemplatesContainer"] ?? string.Empty;
        }

        public async Task<GetPrescriptionSettingsResponseModel> Handle(GetPrescriptionSettingsRequestModel request, CancellationToken cancellationToken)
        {
            GetPrescriptionSettingsResponseModel response = new();
            try
            {
                var existingDoctor = await _context.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (existingDoctor == null)
                {
                    response.Success = false;
                    response.Message = "Invalid doctor Id";

                    return response;
                }

                var existingHospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (existingHospital == null)
                {
                    response.Success = false;
                    response.Message = "Invalid hospital Id";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Success = false;
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var prescriptionSettings = await _context.PrescriptionSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ps => ps.DoctorId == request.DoctorId && ps.HospitalId == request.HospitalId, cancellationToken);

                if (prescriptionSettings != null)
                {
                    PriscriptionSettingsDataModel data = new()
                    {
                        PrescriptionSettingsId = prescriptionSettings.PrescriptionSettingId,
                        DoctorId = prescriptionSettings.DoctorId,
                        HospitalId = prescriptionSettings.HospitalId,
                        HeaderHeight = prescriptionSettings.HeaderHeight ?? 0,
                        FooterHeight = prescriptionSettings.FooterHeight ?? 0,
                        ContentLeftMargin = prescriptionSettings.ContentLeftMargin ?? 0,
                        ContentRightMargin = prescriptionSettings.ContentRightMargin ?? 0,
                        OverFlowPage = prescriptionSettings.OverFlowPage ?? false,
                        FontFamily = prescriptionSettings.FontFamily,
                        FontSize = prescriptionSettings.FontSize ?? 0,
                        FontWeight = prescriptionSettings.FontWeight,
                        TextColour = prescriptionSettings.TextColour,
                        URI = prescriptionSettings.URI,
                        CreatedAtUtc = prescriptionSettings.CreatedAt,
                        UpdatedAtUtc = prescriptionSettings.UpdatedAt,
                        ValidUpto = prescriptionSettings.ValidDuration,
                        UseSystemDefaultLetterhead = prescriptionSettings.UseSystemDefaultLetterhead,
                    };

                    // Re-sign the template URL from its object key so it never goes stale
                    // (S3/MinIO presigned URLs expire within 7 days; Azure returns it unchanged).
                    data.URI = await _blobStorageService.RefreshUrlAsync(
                        _templatesContainer,
                        $"{prescriptionSettings.DoctorId}_{prescriptionSettings.HospitalId}_{_templatesContainer}",
                        prescriptionSettings.URI,
                        cancellationToken);

                    response.Success = true;
                    response.Message = "Prescription settings retrieved successfully.";
                    response.Data = data;
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while retrieving prescription settings: {ex.Message}";
                response.Data = null;
            }

            return response;
        }
    }
}
