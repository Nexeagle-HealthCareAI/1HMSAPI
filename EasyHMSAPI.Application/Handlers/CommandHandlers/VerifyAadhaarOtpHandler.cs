using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyAadhaarOtpHandler : IRequestHandler<VerifyAadhaarOtpRequestModel, AbdmEnrollResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public VerifyAadhaarOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
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
                    MobileVerified = result.MobileVerified,
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
