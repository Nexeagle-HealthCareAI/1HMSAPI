using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RequestUpdateMobileOtpHandler : IRequestHandler<RequestUpdateMobileOtpRequestModel, AbdmOtpTxnResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public RequestUpdateMobileOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmOtpTxnResponseModel> Handle(RequestUpdateMobileOtpRequestModel request, CancellationToken cancellationToken)
        {
            var mobile = (request.NewMobile ?? string.Empty).Replace(" ", string.Empty);
            if (string.IsNullOrWhiteSpace(request.SessionTxnId) || mobile.Length != 10 || !mobile.All(char.IsDigit))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "A valid 10-digit mobile number is required." };

            try
            {
                var result = await _abha.RequestUpdateMobileOtpAsync(request.SessionTxnId, mobile, cancellationToken);
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent to the new mobile number.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
