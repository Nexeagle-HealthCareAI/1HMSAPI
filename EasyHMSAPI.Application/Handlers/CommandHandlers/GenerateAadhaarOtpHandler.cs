using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GenerateAadhaarOtpHandler : IRequestHandler<GenerateAadhaarOtpRequestModel, AbdmOtpTxnResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public GenerateAadhaarOtpHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmOtpTxnResponseModel> Handle(GenerateAadhaarOtpRequestModel request, CancellationToken cancellationToken)
        {
            var aadhaar = (request.AadhaarNumber ?? string.Empty).Replace(" ", string.Empty);
            if (aadhaar.Length != 12 || !aadhaar.All(char.IsDigit))
                return new AbdmOtpTxnResponseModel { Success = false, Message = "Enter a valid 12-digit Aadhaar number." };

            try
            {
                var result = await _abha.GenerateAadhaarOtpAsync(aadhaar, cancellationToken);
                return new AbdmOtpTxnResponseModel { Success = true, Message = result.Message ?? "OTP sent to the Aadhaar-linked mobile number.", TxnId = result.TxnId };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmOtpTxnResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
