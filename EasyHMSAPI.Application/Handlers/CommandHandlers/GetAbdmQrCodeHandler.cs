using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GetAbdmQrCodeHandler : IRequestHandler<GetAbdmQrCodeRequestModel, AbdmBinaryResponseModel>
    {
        private readonly IAbdmAbhaService _abha;

        public GetAbdmQrCodeHandler(IAbdmAbhaService abha)
        {
            _abha = abha;
        }

        public async Task<AbdmBinaryResponseModel> Handle(GetAbdmQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionTxnId))
                return new AbdmBinaryResponseModel { Success = false, Message = "Session is required." };

            try
            {
                var result = await _abha.GetQrCodeAsync(request.SessionTxnId, cancellationToken);
                return new AbdmBinaryResponseModel { Success = true, Content = result.Content, ContentType = result.ContentType };
            }
            catch (InvalidOperationException ex)
            {
                return new AbdmBinaryResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
