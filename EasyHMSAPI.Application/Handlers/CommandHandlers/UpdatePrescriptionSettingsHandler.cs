using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePrescriptionSettingsHandler : IRequestHandler<UpdatePrescriptionSettingsRequestModel, UpdatePrescriptionSettingsResponseModel>
    {
        private readonly AppDbContext _dbContext;
        public UpdatePrescriptionSettingsHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UpdatePrescriptionSettingsResponseModel> Handle(UpdatePrescriptionSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var existingSettings = await _dbContext.PrescriptionSettings
                .FirstOrDefaultAsync(x => x.DoctorId == request.DoctorId, cancellationToken);
            UpdatePrescriptionSettingsResponseModel response = new();
            
            if(existingSettings != null)
            {
                var pageLayoutJson = JsonSerializer.Serialize(request.Settings.PageLayout);
                var letterheadSettingsJson = JsonSerializer.Serialize(new
                {
                    request?.Settings.UseLetterhead,
                    request?.Settings?.LetterheadSettings?.HeaderHeight,
                    request?.Settings?.LetterheadSettings?.FooterHeight
                });
                var headerSettingsJson = JsonSerializer.Serialize(new
                {
                    request?.Settings.UseHeaderSettings,
                    request?.Settings?.HeaderSettings?.Height,
                    request?.Settings?.HeaderSettings?.Width,
                    request?.Settings?.HeaderSettings?.ShowImage,
                    request?.Settings?.HeaderSettings?.ShowOnAllPages
                });
                var footerSettingsJson = JsonSerializer.Serialize(new
                {
                    request?.Settings.UseFooterSettings,
                    request?.Settings?.FooterSettings?.Height,
                    request?.Settings?.FooterSettings?.Width,
                    request?.Settings?.FooterSettings?.ShowImage,
                    request?.Settings?.FooterSettings?.ShowOnAllPages,
                    request?.Settings.UseDoctorSetting,
                    request?.Settings?.DoctorSetting?.ShowSignature,
                    request?.Settings?.DoctorSetting?.SignatureHeight,
                    request?.Settings?.DoctorSetting?.SignatureWidth,
                    request?.Settings?.DoctorSetting?.DoctorName
                });

                existingSettings.PageLayoutJson = pageLayoutJson;
                existingSettings.LetterheadSettingsJson = letterheadSettingsJson;
                existingSettings.HeaderSettingsJson = headerSettingsJson;
                existingSettings.FooterSettingsJson = footerSettingsJson;
                existingSettings.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Settings updated successfully";
            }
            else
            {
                response.Success = false;
                response.Message = "No existing settings found for the doctor.";
            }
            
            return response;
        }
    }
}
