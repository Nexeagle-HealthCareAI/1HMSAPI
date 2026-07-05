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
    /// <summary>Mirrors GetPrescriptionSettingsHandler, adapted to Discharge (no ValidUpto).</summary>
    public class GetDischargeSettingsHandler : IRequestHandler<GetDischargeSettingsRequestModel, GetDischargeSettingsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _templatesContainer;

        public GetDischargeSettingsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _templatesContainer = configuration["BlobStorage:DischargeTemplatesContainer"] ?? string.Empty;
        }

        public async Task<GetDischargeSettingsResponseModel> Handle(GetDischargeSettingsRequestModel request, CancellationToken cancellationToken)
        {
            GetDischargeSettingsResponseModel response = new();
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

                var dischargeSettings = await _context.DischargeSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ds => ds.DoctorId == request.DoctorId && ds.HospitalId == request.HospitalId, cancellationToken);

                if (dischargeSettings != null)
                {
                    DischargeSettingsDataModel data = new()
                    {
                        DischargeSettingId = dischargeSettings.DischargeSettingId,
                        DoctorId = dischargeSettings.DoctorId,
                        HospitalId = dischargeSettings.HospitalId,
                        HeaderHeight = dischargeSettings.HeaderHeight ?? 0,
                        FooterHeight = dischargeSettings.FooterHeight ?? 0,
                        ContentLeftMargin = dischargeSettings.ContentLeftMargin ?? 0,
                        ContentRightMargin = dischargeSettings.ContentRightMargin ?? 0,
                        OverFlowPage = dischargeSettings.OverFlowPage ?? false,
                        FontFamily = dischargeSettings.FontFamily,
                        FontSize = dischargeSettings.FontSize ?? 0,
                        FontWeight = dischargeSettings.FontWeight,
                        TextColour = dischargeSettings.TextColour,
                        URI = dischargeSettings.URI,
                        CreatedAtUtc = dischargeSettings.CreatedAt,
                        UpdatedAtUtc = dischargeSettings.UpdatedAt,
                    };

                    // Re-sign the template URL from its object key so it never goes stale
                    // (S3/MinIO presigned URLs expire within 7 days; Azure returns it unchanged).
                    data.URI = await _blobStorageService.RefreshUrlAsync(
                        _templatesContainer,
                        $"{dischargeSettings.DoctorId}_{dischargeSettings.HospitalId}_{_templatesContainer}",
                        dischargeSettings.URI,
                        cancellationToken);

                    response.Success = true;
                    response.Message = "Discharge settings retrieved successfully.";
                    response.Data = data;
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while retrieving discharge settings: {ex.Message}";
                response.Data = null;
            }

            return response;
        }
    }
}
