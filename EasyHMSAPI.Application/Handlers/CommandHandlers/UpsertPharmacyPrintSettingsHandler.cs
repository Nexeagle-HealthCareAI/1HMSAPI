using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPharmacyPrintSettingsHandler : IRequestHandler<UpsertPharmacyPrintSettingsRequestModel, UpsertPharmacyPrintSettingsResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertPharmacyPrintSettingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertPharmacyPrintSettingsResponseModel> Handle(UpsertPharmacyPrintSettingsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new UpsertPharmacyPrintSettingsResponseModel { Success = false, Message = "HospitalId is required." };

                var now = DateTime.UtcNow;
                var settings = await _context.PharmacyPrintSettings
                    .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

                if (settings == null)
                {
                    settings = new PharmacyPrintSettings
                    {
                        PharmacyPrintSettingsId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        CreatedAt = now,
                    };
                    _context.PharmacyPrintSettings.Add(settings);
                }

                settings.TradeName = request.TradeName;
                settings.Dl20BNumber = request.Dl20BNumber;
                settings.Dl21BNumber = request.Dl21BNumber;
                settings.FssaiNumber = request.FssaiNumber;
                settings.PharmacistName = request.PharmacistName;
                settings.PharmacistRegNo = request.PharmacistRegNo;
                settings.ReturnPolicyText = request.ReturnPolicyText;
                settings.ShowVerificationQr = request.ShowVerificationQr;
                settings.UpdatedAt = now;
                settings.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new UpsertPharmacyPrintSettingsResponseModel { Success = true, Message = "Pharmacy print settings saved." };
            }
            catch (Exception)
            {
                return new UpsertPharmacyPrintSettingsResponseModel { Success = false, Message = "Error saving pharmacy print settings." };
            }
        }
    }
}
