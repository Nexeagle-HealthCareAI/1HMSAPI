using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPublicPrescriptionAttachmentHandler : IRequestHandler<GetPublicPrescriptionAttachmentRequestModel, GetPublicPrescriptionAttachmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public GetPublicPrescriptionAttachmentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }

        public async Task<GetPublicPrescriptionAttachmentResponseModel> Handle(GetPublicPrescriptionAttachmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.AttachmentId == Guid.Empty)
                    return new GetPublicPrescriptionAttachmentResponseModel { Success = false, Message = "Invalid link." };

                var attachment = await _context.PrescriptionAttachments
                    .FirstOrDefaultAsync(a => a.AttachmentId == request.AttachmentId, cancellationToken);
                // Also gated on ReportType -- this is a bare-GUID-keyed anonymous lookup, so
                // without this check it would let anyone with a prescription's QR/link enumerate
                // other attachment types (lab reports, etc.) filed under the same appointment.
                if (attachment == null || !string.Equals(attachment.ReportType, "Prescription", StringComparison.OrdinalIgnoreCase))
                    return new GetPublicPrescriptionAttachmentResponseModel { Success = false, Message = "No prescription is available for this link." };

                var url = await _blobStorageService.RefreshUrlAsync(_containerName, $"{attachment.AttachmentId}_", attachment.StorageUrl, cancellationToken);
                if (string.IsNullOrEmpty(url))
                    return new GetPublicPrescriptionAttachmentResponseModel { Success = false, Message = "The document could not be found." };

                return new GetPublicPrescriptionAttachmentResponseModel { Success = true, RedirectUrl = url, FileName = attachment.FileName };
            }
            catch (Exception)
            {
                return new GetPublicPrescriptionAttachmentResponseModel { Success = false, Message = "Error loading the prescription." };
            }
        }
    }
}
