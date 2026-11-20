using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyAadhaarOtpHandler : IRequestHandler<VerifyAadhaarOtpRequestModel, AbdmEnrollResponseModel>
    {
        private readonly IAbdmAbhaService _abha;
        private readonly IAbhaLinkProofStore _proofs;

        public VerifyAadhaarOtpHandler(IAbdmAbhaService abha, IAbhaLinkProofStore proofs)
        {
            _abha = abha;
            _proofs = proofs;
        }

        public async Task<AbdmEnrollResponseModel> Handle(VerifyAadhaarOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmEnrollResponseModel { Success = false, Message = "Transaction and OTP are required." };
            if (string.IsNullOrWhiteSpace(request.Mobile))
                return new AbdmEnrollResponseModel { Success = false, Message = "A mobile number is required." };

            try
            {
                var result = await _abha.VerifyAadhaarOtpAsync(request.TxnId, request.Otp, request.Mobile, cancellationToken);
                // Proof that this ABHA was verified here, for registering the patient from it.
                string? linkToken = null;
                if (request.CallerUserId != Guid.Empty && request.HospitalId != Guid.Empty && !string.IsNullOrWhiteSpace(result.AbhaNumber))
                    linkToken = _proofs.Issue(request.CallerUserId, request.HospitalId, AbhaPatientRules.ToVerified(result));
                return AbhaPatientRules.EnrolResponse(result, result.MobileVerified, linkToken);
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmEnrollResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
