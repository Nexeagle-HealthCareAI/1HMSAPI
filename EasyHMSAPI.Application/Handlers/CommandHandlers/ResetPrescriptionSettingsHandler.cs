using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class ResetPrescriptionSettingsHandler : IRequestHandler<ResetPrescriptionSettingsRequestModel, ResetPrescriptionSettingsResponseModel>
    {
        private readonly AppDbContext _dbContext;
        public ResetPrescriptionSettingsHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ResetPrescriptionSettingsResponseModel> Handle(ResetPrescriptionSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var existingSetting = await _dbContext.PrescriptionSettings
                .FirstOrDefaultAsync(x => x.DoctorId == request.DoctorId, cancellationToken);
            ResetPrescriptionSettingsResponseModel response = new();


            if (existingSetting != null)
            {
                var defaultSettings = new PrescriptionSettingsDataModel
                {
                    PageLayout = new PageLayoutDataModel
                    {
                        Orientation = "portrait",
                        Margin = new MarginDataModel
                        {
                            Top = 15,
                            Right = 15,
                            Bottom = 15,
                            Left = 15
                        }
                    },
                    UseLetterhead = true,
                    LetterheadSettings = new LetterheadSettingsDataModel { HeaderHeight = 30, FooterHeight = 20 },
                    UseHeaderSettings = false,
                    HeaderSettings = new HeaderSettingsDataModel
                    {
                        Height = 0,
                        Width = 0,
                        ShowImage = false,
                        ShowOnAllPages = false
                    },
                    UseFooterSettings = false,
                    FooterSettings = new FooterSettingsDataModel
                    {
                        Height = 0,
                        Width = 0,
                        ShowImage = false,
                        ShowOnAllPages = false
                    },
                    UseDoctorSetting = false,
                    DoctorSetting = new DoctorSettingDataModel
                    {
                        ShowSignature = false,
                        SignatureHeight = 0,
                        SignatureWidth = 0,
                        DoctorName = string.Empty
                    }
                };

                var pageLayoutJson = JsonSerializer.Serialize(defaultSettings.PageLayout);
                var letterheadSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseLetterhead,
                    defaultSettings.LetterheadSettings.HeaderHeight,
                    defaultSettings.LetterheadSettings.FooterHeight,
                });
                var headerSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseHeaderSettings,
                    defaultSettings.HeaderSettings.Height,
                    defaultSettings.HeaderSettings.Width,
                    defaultSettings.HeaderSettings.ShowImage,
                    defaultSettings.HeaderSettings.ShowOnAllPages
                });
                var footerSettingsJson = JsonSerializer.Serialize(new
                {
                    defaultSettings.UseFooterSettings,
                    defaultSettings.FooterSettings.Height,
                    defaultSettings.FooterSettings.Width,
                    defaultSettings.FooterSettings.ShowImage,
                    defaultSettings.FooterSettings.ShowOnAllPages,
                    defaultSettings.DoctorSetting.ShowSignature,
                    defaultSettings.DoctorSetting.SignatureHeight,
                    defaultSettings.DoctorSetting.SignatureWidth,
                    defaultSettings.DoctorSetting.DoctorName
                });

                existingSetting.PageLayoutJson = pageLayoutJson;
                existingSetting.LetterheadSettingsJson = letterheadSettingsJson;
                existingSetting.HeaderSettingsJson = headerSettingsJson;
                existingSetting.FooterSettingsJson = footerSettingsJson;
                existingSetting.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Settings reset to default successfully";
            }
            else
            {
                response.Success = false;
                response.Message = "No existing settings found to reset";
            }

            return response;
        }
    }
}
