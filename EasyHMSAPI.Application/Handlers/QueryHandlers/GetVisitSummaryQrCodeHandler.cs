using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetVisitSummaryQrCodeHandler : IRequestHandler<GetVisitSummaryQrCodeRequestModel, GetVisitSummaryQrCodeResponseModel>
    {
        private readonly IConfiguration _configuration;

        public GetVisitSummaryQrCodeHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<GetVisitSummaryQrCodeResponseModel> Handle(GetVisitSummaryQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.AppointmentId == Guid.Empty)
                return Task.FromResult(new GetVisitSummaryQrCodeResponseModel { Success = false, Message = "AppointmentId is required." });

            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return Task.FromResult(new GetVisitSummaryQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." });

            var checkInUrl = $"{baseUrl.TrimEnd('/')}/rxv/{request.AppointmentId}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(checkInUrl);

            return Task.FromResult(new GetVisitSummaryQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" });
        }
    }
}
