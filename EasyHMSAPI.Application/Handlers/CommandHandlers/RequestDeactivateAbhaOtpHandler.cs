using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RequestDeactivateAbhaOtpHandler : IRequestHandler<RequestDeactivateAbhaOtpRequestModel, AbdmOtpTxnResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public RequestDeactivateAbhaOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmOtpTxnResponseModel> Handle(RequestDeactivateAbhaOtpRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId) || string.IsNullOrWhiteSpace(request.AbhaNumber))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "Session and ABHA number are required." };

            try
            {
                var result = await _abha.RequestDeactivateOtpAsync(request.SessionTxnId, request.AbhaNumber, request.OtpSystem, cancellationToken);
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
