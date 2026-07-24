using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadHealthLockerDocumentHandler : IRequestHandler<UploadHealthLockerDocumentRequestModel, UploadHealthLockerDocumentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public UploadHealthLockerDocumentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<UploadHealthLockerDocumentResponseModel> Handle(UploadHealthLockerDocumentRequestModel request, CancellationToken cancellationToken)
        {
            var response = new UploadHealthLockerDocumentResponseModel();

            // Optional tag to a past appointment — only allowed when it's genuinely this patient's
            // own (same ownership check GetPublicAppointmentDocumentsHandler uses), otherwise reject
            // rather than silently drop it, so the patient isn't misled about what got saved.
            if (request.ApptId.HasValue)
            {
                var appointmentPatientId = await _context.Appointments
                    .Where(a => a.ApptId == request.ApptId.Value)
                    .Select(a => a.PatientId)
                    .FirstOrDefaultAsync(cancellationToken);

                var isOwner = !string.IsNullOrEmpty(appointmentPatientId) && await _context.PatientRegistrations
                    .AnyAsync(p => p.PatientId == appointmentPatientId && p.Mobile == request.Mobile, cancellationToken);

                if (!isOwner)
                {
                    response.Message = "Appointment not found.";
                    return response;
                }
            }

            var newDocumentId = Guid.NewGuid();
            var uploadResult = await _blobStorageService.UploadAsync(newDocumentId.ToString(), request.File, _containerName, cancellationToken);

            if (string.IsNullOrEmpty(uploadResult))
            {
                response.Message = "Failed to upload the file.";
                return response;
            }

            // Format: "blobName|sasUrl" (see UploadPrescriptionAttachmentsHandler).
            var urlParts = uploadResult.Split('|');
            var blobName = urlParts.Length > 0 ? urlParts[0] : string.Empty;
            var fileUrl = urlParts.Length > 1 ? urlParts[1] : uploadResult;

            var document = new PatientHealthLockerDocument
            {
                DocumentId = newDocumentId,
                Mobile = request.Mobile,
                ApptId = request.ApptId,
                DocumentType = request.DocumentType,
                StorageUrl = fileUrl,
                FileName = !string.IsNullOrEmpty(blobName) ? blobName : request.FileName,
                Notes = request.Notes,
                UploadedAt = DateTime.UtcNow,
            };
            _context.PatientHealthLockerDocuments.Add(document);
            await _context.SaveChangesAsync(cancellationToken);

            response.Success = true;
            response.Message = "Document uploaded successfully.";
            response.DocumentId = newDocumentId;
            response.FileUrl = fileUrl;
            return response;
        }
    }
}
