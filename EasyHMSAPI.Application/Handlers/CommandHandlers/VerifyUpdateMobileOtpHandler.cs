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

                // ABDM's own record is updated regardless (that already happened above) — but if there's
                // no local AbhaAccount row for this (HospitalId, AbhaNumber), say so explicitly instead
                // of silently reporting a plain success that implies everything, including the local
                // copy, is now in sync.
                var message = account == null
                    ? "Mobile number updated on ABDM, but no local ABHA record was found to update at this hospital."
                    : "Mobile number updated.";
                return new AbdmUpdateResponseModel { Success = true, Message = message, Mobile = profile.Mobile };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmUpdateResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
