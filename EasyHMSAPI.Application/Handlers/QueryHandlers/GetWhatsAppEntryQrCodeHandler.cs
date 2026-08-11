using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetWhatsAppEntryQrCodeHandler : IRequestHandler<GetWhatsAppEntryQrCodeRequestModel, GetWhatsAppEntryQrCodeResponseModel>
    {
        private readonly IConfiguration _configuration;

        public GetWhatsAppEntryQrCodeHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<GetWhatsAppEntryQrCodeResponseModel> Handle(GetWhatsAppEntryQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return Task.FromResult(new GetWhatsAppEntryQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." });

            var startUrl = $"{baseUrl.TrimEnd('/')}/start";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(startUrl);

            return Task.FromResult(new GetWhatsAppEntryQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" });
        }
    }
}
