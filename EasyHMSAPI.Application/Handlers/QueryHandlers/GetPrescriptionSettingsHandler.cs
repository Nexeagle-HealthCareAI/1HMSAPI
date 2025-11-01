using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPrescriptionSettingsHandler : IRequestHandler<GetPrescriptionSettingsRequestModel, GetPrescriptionSettingsResponseModel>
    {
        private readonly AppDbContext _dbContext;
        public GetPrescriptionSettingsHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetPrescriptionSettingsResponseModel> Handle(GetPrescriptionSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var prescriptionSettings = await _dbContext.PrescriptionSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DoctorId == request.DoctorId, cancellationToken);
            GetPrescriptionSettingsResponseModel response = new();

            if (prescriptionSettings == null)
            {
                response.Success = false;
                response.PrescriptionSettingsId = null;
                response.DoctorId = request.DoctorId;
                response.Message = "Prescription settings not found for the doctor.";
                response.Settings = null;
            }
            else
            {
                response.Success = true;
                response.PrescriptionSettingsId = prescriptionSettings.PrescriptionSettingId;
                response.DoctorId = prescriptionSettings.DoctorId;
                response.Message = "Prescription settings retrieved successfully.";

                var settingsDataModel = new PrescriptionSettingsDataModel();
                DoctorSettingDataModel? doctorSetting = null;
                if (!string.IsNullOrEmpty(prescriptionSettings.PageLayoutJson))
                {
                    settingsDataModel.PageLayout = JsonSerializer.Deserialize<PageLayoutDataModel>(prescriptionSettings.PageLayoutJson);
                }
                if (!string.IsNullOrEmpty(prescriptionSettings.LetterheadSettingsJson))
                {
                    settingsDataModel.LetterheadSettings = JsonSerializer.Deserialize<LetterheadSettingsDataModel>(prescriptionSettings.LetterheadSettingsJson);
                    using var doc = JsonDocument.Parse(prescriptionSettings.LetterheadSettingsJson);
                    var root = doc.RootElement;
                    settingsDataModel.UseLetterhead = root.TryGetProperty("UseLetterhead", out var useLetterhead) && useLetterhead.GetBoolean();
                }
                if (!string.IsNullOrEmpty(prescriptionSettings.HeaderSettingsJson))
                {
                    settingsDataModel.HeaderSettings = JsonSerializer.Deserialize<HeaderSettingsDataModel>(prescriptionSettings.HeaderSettingsJson);
                    using var doc = JsonDocument.Parse(prescriptionSettings.HeaderSettingsJson);
                    var root = doc.RootElement;
                    settingsDataModel.UseHeaderSettings = root.TryGetProperty("UseHeaderSettings", out var useHeaderSettings) && useHeaderSettings.GetBoolean();
                }
                if (!string.IsNullOrEmpty(prescriptionSettings.FooterSettingsJson))
                {
                    using var doc = JsonDocument.Parse(prescriptionSettings.FooterSettingsJson);
                    var root = doc.RootElement;
                    settingsDataModel.FooterSettings = JsonSerializer.Deserialize<FooterSettingsDataModel>(prescriptionSettings.FooterSettingsJson);
                    settingsDataModel.UseFooterSettings = root.TryGetProperty("UseFooterSettings", out var useFooterSettings) && useFooterSettings.GetBoolean();
                    settingsDataModel.UseDoctorSetting = root.TryGetProperty("UseDoctorSetting", out var useDoctorSetting) && useDoctorSetting.GetBoolean();
                    doctorSetting = new DoctorSettingDataModel
                    {
                        ShowSignature = root.TryGetProperty("ShowSignature", out var showSignature) && showSignature.GetBoolean(),
                        SignatureHeight = root.TryGetProperty("SignatureHeight", out var signatureHeight) ? signatureHeight.GetInt32() : 0,
                        SignatureWidth = root.TryGetProperty("SignatureWidth", out var signatureWidth) ? signatureWidth.GetInt32() : 0,
                        DoctorName = root.TryGetProperty("DoctorName", out var doctorName) ? doctorName.GetString() : string.Empty
                    };
                    settingsDataModel.DoctorSetting = doctorSetting;
                }

                response.Settings = settingsDataModel;
            }

            return response;
        }
    }
}
