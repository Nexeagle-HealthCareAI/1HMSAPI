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
                var result = await _abha.VerifyLoginOtpAsync(request.TxnId, request.Otp, request.LoginHint, cancellationToken);
                string? linkToken = null;
                if (request.CallerUserId != Guid.Empty && request.HospitalId != Guid.Empty && !string.IsNullOrWhiteSpace(result.AbhaNumber))
                    linkToken = _proofs.Issue(request.CallerUserId, request.HospitalId,
                        new VerifiedAbhaProfile(result.AbhaNumber!, result.AbhaAddress, result.FullName, result.Gender, result.DateOfBirth, result.Mobile));
                return new AbdmProfileResponseModel
                {
                    LinkToken = linkToken,
                    Success = true,
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
