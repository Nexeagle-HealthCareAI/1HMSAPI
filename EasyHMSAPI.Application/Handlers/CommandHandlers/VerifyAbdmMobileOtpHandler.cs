using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyAbdmMobileOtpHandler : IRequestHandler<VerifyAbdmMobileOtpRequestModel, AbdmEnrollResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public VerifyAbdmMobileOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmEnrollResponseModel> Handle(VerifyAbdmMobileOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmEnrollResponseModel { Success = false, Message = "Transaction and OTP are required." };

            try
            {
                var result = await _abha.VerifyMobileOtpAsync(request.TxnId, request.Otp, cancellationToken);
                return new AbdmEnrollResponseModel
                {
                    Success = true,
                    TxnId = result.TxnId,
                    AbhaNumber = result.AbhaNumber,
                    AbhaAddress = result.AbhaAddress,
                    FullName = result.FullName,
                    Gender = result.Gender,
                    DateOfBirth = result.DateOfBirth,
                    Mobile = result.Mobile,
                    MobileVerified = true,
                    IsNew = result.IsNew
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmEnrollResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
