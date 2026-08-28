using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Mirrors UpdatePrescriptionSettingsHandler, adapted to Discharge (no ValidUpto).</summary>
    public class UpdateDischargeSettingsHandler : IRequestHandler<UpdateDischargeSettingsRequestModel, UpdateDischargeSettingsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        public UpdateDischargeSettingsHandler(AppDbContext dbContext, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = dbContext;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<UpdateDischargeSettingsResponseModel> Handle(UpdateDischargeSettingsRequestModel request, CancellationToken cancellationToken)
        {
            UpdateDischargeSettingsResponseModel response = new();
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

                if (!string.IsNullOrEmpty(request.TextColour) && !TextColourValidator.IsValid(request.TextColour))
                {
                    response.Success = false;
                    response.Message = "Text colour must be a hex value like #111827 or #111827FF.";
                    return response;
                }

                var existingSettings = await _context.DischargeSettings
                    .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId && x.DoctorId == request.DoctorId, cancellationToken);
                var currentDateTime = DateTime.UtcNow;
                var newDischargeSettingId = Guid.NewGuid();

                if (existingSettings == null)
                {
                    DischargeSetting newSettings = new()
                    {
                        DischargeSettingId = newDischargeSettingId,
                        HospitalId = request.HospitalId,
                        DoctorId = request.DoctorId,
                        HeaderHeight = request.HeaderHeight,
                        FooterHeight = request.FooterHeight,
                        ContentLeftMargin = request.ContentLeftMargin,
                        ContentRightMargin = request.ContentRightMargin,
                        OverFlowPage = request.OverFlowPage,
                        FontFamily = request.FontFamily,
                        FontSize = request.FontSize,
                        FontWeight = request.FontWeight,
                        TextColour = request.TextColour,
                        CreatedAt = currentDateTime,
                        UpdatedAt = currentDateTime,
                        CreatedByUserId = request.LoggedInUserId,
                        UseSystemDefaultLetterhead = request.UseSystemDefaultLetterhead ?? false,
                    };
                    _context.DischargeSettings.Add(newSettings);

                    response.DischargeSettingId = newDischargeSettingId;
                    response.Success = true;
                    response.Message = "Discharge settings created successfully.";
                }
                else
                {
                    if (request.HeaderHeight.HasValue)
                        existingSettings.HeaderHeight = request.HeaderHeight.Value;
                    if (request.FooterHeight.HasValue)
                        existingSettings.FooterHeight = request.FooterHeight.Value;
                    if (request.ContentLeftMargin.HasValue)
                        existingSettings.ContentLeftMargin = request.ContentLeftMargin.Value;
                    if (request.ContentRightMargin.HasValue)
                        existingSettings.ContentRightMargin = request.ContentRightMargin.Value;
                    if (request.OverFlowPage.HasValue)
                        existingSettings.OverFlowPage = request.OverFlowPage.Value;
                    if (!string.IsNullOrEmpty(request.FontFamily))
                        existingSettings.FontFamily = request.FontFamily;
                    if (request.FontSize.HasValue)
                        existingSettings.FontSize = request.FontSize.Value;
                    if (!string.IsNullOrEmpty(request.FontWeight))
                        existingSettings.FontWeight = request.FontWeight;
                    if (!string.IsNullOrEmpty(request.TextColour))
                        existingSettings.TextColour = request.TextColour;
                    if (request.UseSystemDefaultLetterhead.HasValue)
                        existingSettings.UseSystemDefaultLetterhead = request.UseSystemDefaultLetterhead.Value;
                    existingSettings.UpdatedAt = currentDateTime;

                    response.DischargeSettingId = existingSettings.DischargeSettingId;
                    response.Success = true;
                    response.Message = "Discharge settings updated successfully.";
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while updating discharge settings: {ex.Message}";
            }

            return response;
        }
    }
}
