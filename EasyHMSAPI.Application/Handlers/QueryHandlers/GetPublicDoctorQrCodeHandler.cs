using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPublicDoctorQrCodeHandler : IRequestHandler<GetPublicDoctorQrCodeRequestModel, GetPublicDoctorQrCodeResponseModel>
    {
        private readonly IConfiguration _configuration;

        public GetPublicDoctorQrCodeHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<GetPublicDoctorQrCodeResponseModel> Handle(GetPublicDoctorQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return Task.FromResult(new GetPublicDoctorQrCodeResponseModel { Success = false, Message = "DoctorId is required." });

            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return Task.FromResult(new GetPublicDoctorQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." });

            var bookingUrl = $"{baseUrl.TrimEnd('/')}/doc/{request.DoctorId}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(bookingUrl);

            return Task.FromResult(new GetPublicDoctorQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" });
        }
    }
}
