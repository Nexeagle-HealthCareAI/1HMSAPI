using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalBookingQrCodeHandler : IRequestHandler<GetHospitalBookingQrCodeRequestModel, GetHospitalBookingQrCodeResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public GetHospitalBookingQrCodeHandler(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<GetHospitalBookingQrCodeResponseModel> Handle(GetHospitalBookingQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            var hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (hospital == null)
                return new GetHospitalBookingQrCodeResponseModel { Success = false, Message = "Hospital not found." };

            if (string.IsNullOrEmpty(hospital.HospitalCode))
                return new GetHospitalBookingQrCodeResponseModel { Success = false, Message = "Generate a hospital code first." };

            // Same config key GetHospitalQrCodeHandler (check-in QR) already reads -- the bot's
            // public base URL, e.g. https://whatsapp-dev-api.nexeagle.com.
            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return new GetHospitalBookingQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." };

            // "/h/" (hospital booking), not "/c/" (OPD check-in) -- see
            // app/front_door/qr_redirects.py's hospital_booking_qr_redirect in the WhatsApp bot.
            var bookingUrl = $"{baseUrl.TrimEnd('/')}/h/{hospital.HospitalCode}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(bookingUrl);

            return new GetHospitalBookingQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" };
        }
    }
}
