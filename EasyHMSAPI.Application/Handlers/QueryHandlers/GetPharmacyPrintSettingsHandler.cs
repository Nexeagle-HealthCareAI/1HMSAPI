using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPharmacyPrintSettingsHandler : IRequestHandler<GetPharmacyPrintSettingsRequestModel, GetPharmacyPrintSettingsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPharmacyPrintSettingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPharmacyPrintSettingsResponseModel> Handle(GetPharmacyPrintSettingsRequestModel request, CancellationToken cancellationToken)
        {
            var gstin = await _context.Hospitals.AsNoTracking()
                .Where(h => h.HospitalID == request.HospitalId)
                .Select(h => h.GSTIN)
                .FirstOrDefaultAsync(cancellationToken);

            var settings = await _context.PharmacyPrintSettings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

            if (settings == null)
            {
                return new GetPharmacyPrintSettingsResponseModel { Configured = false, HospitalGstin = gstin, ShowVerificationQr = true };
            }

            return new GetPharmacyPrintSettingsResponseModel
            {
                Configured = true,
                TradeName = settings.TradeName,
                Dl20BNumber = settings.Dl20BNumber,
                Dl21BNumber = settings.Dl21BNumber,
                FssaiNumber = settings.FssaiNumber,
                PharmacistName = settings.PharmacistName,
                PharmacistRegNo = settings.PharmacistRegNo,
                ReturnPolicyText = settings.ReturnPolicyText,
                ShowVerificationQr = settings.ShowVerificationQr,
                HospitalGstin = gstin,
            };
        }
    }
}
