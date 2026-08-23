using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPrescriptionAttachmentQrCodeHandler : IRequestHandler<GetPrescriptionAttachmentQrCodeRequestModel, GetPrescriptionAttachmentQrCodeResponseModel>
    {
        private readonly IConfiguration _configuration;

        public GetPrescriptionAttachmentQrCodeHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<GetPrescriptionAttachmentQrCodeResponseModel> Handle(GetPrescriptionAttachmentQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.AttachmentId == Guid.Empty)
                return Task.FromResult(new GetPrescriptionAttachmentQrCodeResponseModel { Success = false, Message = "AttachmentId is required." });

            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return Task.FromResult(new GetPrescriptionAttachmentQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." });

            var checkInUrl = $"{baseUrl.TrimEnd('/')}/rx/{request.AttachmentId}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(checkInUrl);

            return Task.FromResult(new GetPrescriptionAttachmentQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" });
        }
    }
}
