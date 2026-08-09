using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyDeactivateAbhaOtpHandler : IRequestHandler<VerifyDeactivateAbhaOtpRequestModel, AbdmUpdateResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public VerifyDeactivateAbhaOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmUpdateResponseModel> Handle(VerifyDeactivateAbhaOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId) || string.IsNullOrWhiteSpace(request.DeactivateTxnId) || string.IsNullOrWhiteSpace(request.Otp))
                return new AbdmUpdateResponseModel { Success = false, Message = "Session, transaction and OTP are required." };

            try
            {
                var result = await _abha.VerifyDeactivateOtpAsync(request.SessionTxnId, request.DeactivateTxnId, request.Otp, request.Reason, cancellationToken);
                return new AbdmUpdateResponseModel { Success = result.Success, Message = result.Message };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmUpdateResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
