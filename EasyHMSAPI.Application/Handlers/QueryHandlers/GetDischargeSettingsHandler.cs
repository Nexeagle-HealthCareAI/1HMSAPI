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
                // DoctorId = Guid.Empty asks for the hospital's own row (hospital letterhead and
                // defaults, edited from Admin → Configuration); doctor checks apply otherwise.
                var hospitalMode = request.DoctorId == Guid.Empty;
                if (!hospitalMode)
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

                if (!hospitalMode && !await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
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
                    response.Success = true;
                    response.Message = "Discharge settings retrieved successfully.";
                    response.Data = await ToDataAsync(dischargeSettings, cancellationToken);
                }
                else
                {
                    // No settings row yet (new doctor) — return success with null data so
                    // the frontend can initialise with defaults without treating it as an error.
                    response.Success = true;
                    response.Message = "No discharge settings found for this doctor.";
                    response.Data = null;
                }

                // A doctor without a letterhead of their own prints on the hospital's letterhead.
                if (!hospitalMode)
                {
                    var hospitalRow = await _context.DischargeSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ds => ds.DoctorId == Guid.Empty && ds.HospitalId == request.HospitalId, cancellationToken);
                    if (hospitalRow != null && !string.IsNullOrWhiteSpace(hospitalRow.URI))
                        response.HospitalDefault = await ToDataAsync(hospitalRow, cancellationToken);
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

        private async Task<DischargeSettingsDataModel> ToDataAsync(Domain.Entities.DischargeSetting row, CancellationToken cancellationToken)
        {
            DischargeSettingsDataModel data = new()
            {
                DischargeSettingId = row.DischargeSettingId,
                DoctorId = row.DoctorId,
                HospitalId = row.HospitalId,
                HeaderHeight = row.HeaderHeight ?? 0,
                FooterHeight = row.FooterHeight ?? 0,
                ContentLeftMargin = row.ContentLeftMargin ?? 0,
                ContentRightMargin = row.ContentRightMargin ?? 0,
                OverFlowPage = row.OverFlowPage ?? false,
                FontFamily = row.FontFamily,
                FontSize = row.FontSize ?? 0,
                FontWeight = row.FontWeight,
                TextColour = row.TextColour,
                URI = row.URI,
                CreatedAtUtc = row.CreatedAt,
                UpdatedAtUtc = row.UpdatedAt,
                UseSystemDefaultLetterhead = row.UseSystemDefaultLetterhead,
                IsHospitalLetterhead = row.DoctorId == Guid.Empty,
            };

            // Re-sign the template URL from its object key so it never goes stale
            // (S3/MinIO presigned URLs expire within 7 days; Azure returns it unchanged).
            // Key matches the upload key in UploadDischargeTemplateHandler: "{DoctorId}_{HospitalId}".
            data.URI = await _blobStorageService.RefreshUrlAsync(
                _templatesContainer,
                $"{row.DoctorId}_{row.HospitalId}",
                row.URI,
                cancellationToken);
            return data;
        }
    }
}
