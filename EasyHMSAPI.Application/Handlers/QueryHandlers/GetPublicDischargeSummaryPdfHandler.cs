using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Backs the fully anonymous "view discharge summary on mobile" link (QR code / WhatsApp
    /// attachment link). Looked up by AccessToken alone -- a long random opaque string, not the
    /// AdmissionId, so the link can't be guessed or enumerated.
    /// </summary>
    public class GetPublicDischargeSummaryPdfHandler : IRequestHandler<GetPublicDischargeSummaryPdfRequestModel, GetPublicDischargeSummaryPdfResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public GetPublicDischargeSummaryPdfHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:DischargeSummaryContainer"] ?? string.Empty;
        }

        public async Task<GetPublicDischargeSummaryPdfResponseModel> Handle(GetPublicDischargeSummaryPdfRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return new GetPublicDischargeSummaryPdfResponseModel { Success = false, Message = "Invalid link." };

                var summary = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.AccessToken == request.AccessToken, cancellationToken);
                if (summary == null || string.IsNullOrEmpty(summary.PdfBlobKey))
                    return new GetPublicDischargeSummaryPdfResponseModel { Success = false, Message = "This link is no longer valid." };

                var url = await _blobStorageService.RefreshUrlAsync(_containerName, summary.PdfBlobKey, null, cancellationToken);
                if (string.IsNullOrEmpty(url))
                    return new GetPublicDischargeSummaryPdfResponseModel { Success = false, Message = "The document could not be found." };

                return new GetPublicDischargeSummaryPdfResponseModel { Success = true, RedirectUrl = url };
            }
            catch (Exception)
            {
                return new GetPublicDischargeSummaryPdfResponseModel { Success = false, Message = "Error loading the discharge summary." };
            }
        }
    }
}
