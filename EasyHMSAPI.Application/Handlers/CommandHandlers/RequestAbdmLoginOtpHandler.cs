using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
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
                return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter a mobile number, Aadhaar number, or ABHA number." };

            try
            {
                var result = await _abha.RequestLoginOtpAsync(request.LoginId, request.LoginHint, request.OtpSystem, cancellationToken);
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
