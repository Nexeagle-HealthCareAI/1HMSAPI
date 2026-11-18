using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyAbdmLoginOtpHandler : IRequestHandler<VerifyAbdmLoginOtpRequestModel, AbdmProfileResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly IAbhaLinkProofStore _proofs;

        public VerifyAbdmLoginOtpHandler(IAbdmAbhaService abha, IAbhaLinkProofStore proofs)
        {
            _abha = abha;
            _proofs = proofs;
        }

        public async Task<AbdmProfileResponseModel> Handle(VerifyAbdmLoginOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmProfileResponseModel { Success = false, Message = "Transaction and OTP are required." };

            try
            {
                AbdmProfileResult result;
                if (string.Equals(request.LoginHint, AbdmLoginRules.HintAbhaAddress, StringComparison.OrdinalIgnoreCase))
                {
                    var address = AbdmLoginRules.NormaliseAbhaAddress(request.LoginId);
                    if (address == null)
                        return new AbdmProfileResponseModel { Success = false, Message = "Enter the ABHA address the OTP was sent for, like name@abdm." };
                    result = await _abha.VerifyAbhaAddressLoginOtpAsync(request.TxnId, request.Otp, request.OtpSystem ?? AbdmLoginRules.OtpAbdm, address, cancellationToken);
                }
                else
                {
                    result = await _abha.VerifyLoginOtpAsync(request.TxnId, request.Otp, request.LoginHint, cancellationToken);
                }
                var byAddress = string.Equals(request.LoginHint, AbdmLoginRules.HintAbhaAddress, StringComparison.OrdinalIgnoreCase);
                // Nothing came back to verify against: never show this as a verified account.
                if (!byAddress && string.IsNullOrWhiteSpace(result.AbhaNumber))
                    return new AbdmProfileResponseModel { Success = false, Message = "ABDM did not return an ABHA account for this OTP. Check the OTP and try again." };
                string? linkToken = null;
                if (request.CallerUserId != Guid.Empty && request.HospitalId != Guid.Empty && !string.IsNullOrWhiteSpace(result.AbhaNumber))
                    linkToken = _proofs.Issue(request.CallerUserId, request.HospitalId,
                        new VerifiedAbhaProfile(result.AbhaNumber!, result.AbhaAddress, result.FullName, result.Gender, result.DateOfBirth, result.Mobile));
                return new AbdmProfileResponseModel
                {
                    LinkToken = linkToken,
                    Success = true,
                    // Verified, but an address with no ABHA number behind it cannot be kept on a patient record.
                    Message = string.IsNullOrWhiteSpace(result.AbhaNumber)
                        ? "Verified, but this ABHA address has no ABHA number linked to it, so it cannot be saved against the patient. Ask the patient to link their ABHA number in the ABHA app, or verify by ABHA number instead."
                        : null,
                    TxnId = result.TxnId,
                    AbhaNumber = result.AbhaNumber,
                    AbhaAddress = result.AbhaAddress,
                    FullName = result.FullName,
                    Gender = result.Gender,
                    DateOfBirth = result.DateOfBirth,
                    Mobile = result.Mobile,
                    Email = result.Email
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmProfileResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
