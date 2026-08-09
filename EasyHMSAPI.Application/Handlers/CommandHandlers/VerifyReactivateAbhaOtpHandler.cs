using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyReactivateAbhaOtpHandler : IRequestHandler<VerifyReactivateAbhaOtpRequestModel, AbdmProfileResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public VerifyReactivateAbhaOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmProfileResponseModel> Handle(VerifyReactivateAbhaOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmProfileResponseModel { Success = false, Message = "Transaction and OTP are required." };

            try
            {
                var result = await _abha.VerifyReactivateOtpAsync(request.TxnId, request.Otp, cancellationToken);
                return new AbdmProfileResponseModel
                {
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
