using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Patient-portal read of the SAME PrescriptionAttachments a doctor uploads during a
    // consultation (see GetPrescriptionAttachmentsHandler) — no new storage, just a
    // mobile-ownership-gated view of it for Doctor Dekho's "my appointments" page.
    public class GetPublicAppointmentDocumentsHandler : IRequestHandler<GetPublicAppointmentDocumentsRequestModel, GetPublicAppointmentDocumentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _attachmentsContainer;

        public GetPublicAppointmentDocumentsHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _attachmentsContainer = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }

        public async Task<GetPublicAppointmentDocumentsResponseModel> Handle(GetPublicAppointmentDocumentsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GetPublicAppointmentDocumentsResponseModel { AppointmentId = request.AppointmentId };

            var appointmentPatientId = await _context.Appointments
                .Where(a => a.ApptId == request.AppointmentId)
                .Select(a => a.PatientId)
                .FirstOrDefaultAsync(cancellationToken);

            // Same "Appointment not found" message whether it genuinely doesn't exist or this
            // mobile just isn't the owner — never confirms/denies existence to an unauthorized caller.
            if (string.IsNullOrEmpty(appointmentPatientId))
            {
                response.Message = "Appointment not found.";
                return response;
            }

            var isOwner = await _context.PatientRegistrations
                .AnyAsync(p => p.PatientId == appointmentPatientId && p.Mobile == request.Mobile, cancellationToken);
            if (!isOwner)
            {
                response.Message = "Appointment not found.";
                return response;
            }

            var attachments = await _context.PrescriptionAttachments
                .Where(pa => pa.ApptId == request.AppointmentId)
                .Select(pa => new PublicAppointmentDocument
                {
                    AttachmentId = pa.AttachmentId,
                    ReportType = pa.ReportType,
                    FileName = pa.FileName,
                    StorageUrl = pa.StorageUrl,
                    Notes = pa.Notes,
                    UploadedAt = pa.UploadedAt,
                })
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync(cancellationToken);

            // Re-sign each URL from its stored object key — S3/MinIO presigned URLs expire within
            // 7 days; matches GetPrescriptionAttachmentsHandler's container-selection rule exactly.
            foreach (var doc in attachments)
            {
                var targetContainer = doc.ReportType?.Equals("Lab Report", StringComparison.OrdinalIgnoreCase) == true
                    ? "labreports"
                    : _attachmentsContainer;

                doc.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                    targetContainer,
                    $"{doc.AttachmentId}_",
                    doc.StorageUrl,
                    cancellationToken);
            }

            response.Success = true;
            response.Message = attachments.Count == 0 ? "No documents found for this appointment." : "Documents retrieved successfully.";
            response.Documents = attachments;
            return response;
        }
    }
}
