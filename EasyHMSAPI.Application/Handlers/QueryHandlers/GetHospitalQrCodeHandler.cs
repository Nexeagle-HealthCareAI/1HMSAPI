using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalQrCodeHandler : IRequestHandler<GetHospitalQrCodeRequestModel, GetHospitalQrCodeResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public GetHospitalQrCodeHandler(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<GetHospitalQrCodeResponseModel> Handle(GetHospitalQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            var hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (hospital == null)
                return new GetHospitalQrCodeResponseModel { Success = false, Message = "Hospital not found." };

            if (string.IsNullOrEmpty(hospital.HospitalCode))
                return new GetHospitalQrCodeResponseModel { Success = false, Message = "Generate a hospital code first." };

            // Same config key WhatsAppQueueNotifier already reads (Phase 1) -- the bot's public
            // base URL, e.g. https://whatsapp-dev-api.nexeagle.com.
            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return new GetHospitalQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." };

            var checkInUrl = $"{baseUrl.TrimEnd('/')}/c/{hospital.HospitalCode}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(checkInUrl);

            return new GetHospitalQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" };
        }
    }
}
