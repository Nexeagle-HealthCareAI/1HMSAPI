using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RequestAbdmLoginOtpHandler : IRequestHandler<RequestAbdmLoginOtpRequestModel, AbdmOtpTxnResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public RequestAbdmLoginOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmOtpTxnResponseModel> Handle(RequestAbdmLoginOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.LoginId))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter a mobile number, Aadhaar number, ABHA number or ABHA address." };
            // Refused here, before anything is sent to ABDM (VRFY_ABHA_401).
            if (string.Equals(request.LoginHint, AbdmLoginRules.HintAadhaar, StringComparison.OrdinalIgnoreCase) && !AbdmLoginRules.IsValidAadhaar(request.LoginId))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "That is not a valid Aadhaar number. Check the 12 digits and try again." };

            try
            {
                AbdmOtpTxnResult result;
                if (string.Equals(request.LoginHint, AbdmLoginRules.HintAbhaAddress, StringComparison.OrdinalIgnoreCase))
                {
                    var address = AbdmLoginRules.NormaliseAbhaAddress(request.LoginId);
                    if (address == null)
                        return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter the ABHA address in full, like name@abdm." };
                    result = await _abha.RequestAbhaAddressLoginOtpAsync(address, request.OtpSystem, cancellationToken);
                }
                else
                {
                    var loginId = request.LoginId.Trim();
                    if (string.Equals(request.LoginHint, AbdmLoginRules.HintAbhaNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        // Encrypted with its dashes: ABDM refuses the bare 14 digits.
                        var dashed = AbdmLoginRules.DashedAbhaNumber(loginId);
                        if (dashed == null)
                            return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter the 14-digit ABHA number." };
                        loginId = dashed;
                    }
                    result = await _abha.RequestLoginOtpAsync(loginId, request.LoginHint, request.OtpSystem, cancellationToken);
                }
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex) when (AbdmLoginRules.IsNoAbha(ex)
                && (string.Equals(request.LoginHint, AbdmLoginRules.HintMobile, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.LoginHint, AbdmLoginRules.HintAadhaar, StringComparison.OrdinalIgnoreCase)))
            {
                return new AbdmOtpTxnResponseModel { Success = false, NotFound = true, Message = AbdmLoginRules.NoAbhaMessage(request.LoginHint) };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
