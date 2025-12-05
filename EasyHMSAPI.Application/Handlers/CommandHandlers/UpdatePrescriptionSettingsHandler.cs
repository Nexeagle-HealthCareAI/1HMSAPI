using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePrescriptionSettingsHandler : IRequestHandler<UpdatePrescriptionSettingsRequestModel, UpdatePrescriptionSettingsResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdatePrescriptionSettingsHandler(AppDbContext dbContext)
        {
            _context = dbContext;
        }

        public async Task<UpdatePrescriptionSettingsResponseModel> Handle(UpdatePrescriptionSettingsRequestModel request, CancellationToken cancellationToken)
        {
            UpdatePrescriptionSettingsResponseModel response = new();
            try
            {
                var existingSettings = await _context.PrescriptionSettings
               .FirstOrDefaultAsync(x => x.HospitalId == request.HospitalId && x.DoctorId == request.DoctorId, cancellationToken);
                var currentDateTime = DateTime.UtcNow;
                var newPrescriptionSettingId = Guid.NewGuid();
               
                if (existingSettings == null)
                {
                    PrescriptionSetting newSettings = new PrescriptionSetting
                    {
                        PrescriptionSettingId = newPrescriptionSettingId,
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
                        CreatedByUserId = request.LoggedInUserId
                    };
                    _context.PrescriptionSettings.Add(newSettings);

                    response.PrescriptionSettingId = newPrescriptionSettingId;
                    response.Success = true;
                    response.Message = "Prescription settings created successfully.";
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
                    existingSettings.UpdatedAt = currentDateTime;

                    response.PrescriptionSettingId = existingSettings.PrescriptionSettingId;
                    response.Success = true;
                    response.Message = "Prescription settings updated successfully.";
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = $"An error occurred while updating prescription settings: {ex.Message}";
            }

            return response;
        }
    }
}
