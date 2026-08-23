using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GetDischargeSummaryQrCodeHandler : IRequestHandler<GetDischargeSummaryQrCodeRequestModel, GetDischargeSummaryQrCodeResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public GetDischargeSummaryQrCodeHandler(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<GetDischargeSummaryQrCodeResponseModel> Handle(GetDischargeSummaryQrCodeRequestModel request, CancellationToken cancellationToken)
        {
            var summary = await _context.DischargeSummary
                .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);
            if (summary == null)
                return new GetDischargeSummaryQrCodeResponseModel { Success = false, Message = "Save the discharge summary before generating a QR code." };

            // Same idempotent mint check as UploadDischargeSummaryPdfHandler -- pre-setting it
            // here (before the PDF exists) is safe because that handler's own check tolerates
            // AccessToken already being set.
            if (string.IsNullOrEmpty(summary.AccessToken))
            {
                summary.AccessToken = RandomNumberGenerator.GetHexString(40);
                summary.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var baseUrl = _configuration["WhatsAppBot:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
                return new GetDischargeSummaryQrCodeResponseModel { Success = false, Message = "WhatsApp bot base URL is not configured." };

            var checkInUrl = $"{baseUrl.TrimEnd('/')}/d/{summary.AccessToken}";
            var pngBytes = QrCodeGenerator.GenerateWithLogo(checkInUrl);

            return new GetDischargeSummaryQrCodeResponseModel { Success = true, Content = pngBytes, ContentType = "image/png" };
        }
    }
}
