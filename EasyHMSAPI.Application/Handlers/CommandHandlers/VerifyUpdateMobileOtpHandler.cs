using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Verifies the OTP for a mobile-number change, then refreshes the canonical profile
    /// from ABDM and persists the new mobile onto the locally-recorded AbhaAccount row.</summary>
    public class VerifyUpdateMobileOtpHandler : IRequestHandler<VerifyUpdateMobileOtpRequestModel, AbdmUpdateResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly AppDbContext _context;

        public VerifyUpdateMobileOtpHandler(IAbdmAbhaService abha, AppDbContext context)
        {
            _abha = abha;
            _context = context;
        }

        public async Task<AbdmUpdateResponseModel> Handle(VerifyUpdateMobileOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId) || string.IsNullOrWhiteSpace(request.UpdateTxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmUpdateResponseModel { Success = false, Message = "Transaction and OTP are required." };

            try
            {
                await _abha.VerifyUpdateMobileOtpAsync(request.SessionTxnId, request.UpdateTxnId, request.Otp, cancellationToken);
                var profile = await _abha.GetProfileAsync(request.SessionTxnId, cancellationToken);

                var account = await _context.AbhaAccount.FirstOrDefaultAsync(
                    a => a.HospitalId == request.HospitalId && a.AbhaNumber == request.AbhaNumber, cancellationToken);
                if (account != null && !string.IsNullOrWhiteSpace(profile.Mobile))
                {
                    account.Mobile = profile.Mobile;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return new AbdmUpdateResponseModel { Success = true, Message = "Mobile number updated.", Mobile = profile.Mobile };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmUpdateResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
