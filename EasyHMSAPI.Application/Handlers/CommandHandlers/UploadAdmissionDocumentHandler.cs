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
    public class UploadAdmissionDocumentHandler : IRequestHandler<UploadAdmissionDocumentRequestModel, UploadAdmissionDocumentResponseModel>
    {
        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx",
        };

        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public UploadAdmissionDocumentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:AdmissionDocumentsContainer"] ?? string.Empty;
        }

        public async Task<UploadAdmissionDocumentResponseModel> Handle(UploadAdmissionDocumentRequestModel request, CancellationToken cancellationToken)
        {
            UploadAdmissionDocumentResponseModel response = new()
            {
                Success = false,
            };
            try
            {
                if (request.File == null || request.File.Length == 0)
                {
                    response.Message = "A file is required.";
                    return response;
                }

                if (request.File.Length > MaxFileSizeBytes)
                {
                    response.Message = "File is too large. Maximum allowed size is 20 MB.";
                    return response;
                }

                var extension = Path.GetExtension(request.File.FileName);
                if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                {
                    response.Message = "Unsupported file type. Allowed types: PDF, JPG, PNG, DOC, DOCX.";
                    return response;
                }

                var admission = await _context.Admission
                    .Where(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (admission == null)
                {
                    response.Message = "Admission not found.";
                    return response;
                }

                var newDocumentId = Guid.NewGuid();
                var uploadResult = await _blobStorageService.UploadAsync(newDocumentId.ToString(), request.File, _containerName, cancellationToken);

                if (string.IsNullOrEmpty(uploadResult))
                {
                    response.Message = "Upload failed. Please try again.";
                    return response;
                }

                // Format: "blobName|presignedUrl" (multi-file container branch).
                var urlParts = uploadResult.Split('|');
                var blobName = urlParts.Length > 0 ? urlParts[0] : string.Empty;
                var fileUrl = urlParts.Length > 1 ? urlParts[1] : uploadResult;

                AdmissionDocument document = new()
                {
                    DocumentId = newDocumentId,
                    HospitalId = request.HospitalId,
                    AdmissionId = request.AdmissionId,
                    DocumentName = request.File.FileName,
                    ContentType = request.File.ContentType,
                    FileSizeBytes = request.File.Length,
                    StorageObjectKey = blobName,
                    StorageUrl = fileUrl,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = request.UploadedByUserName,
                };
                _context.AdmissionDocument.Add(document);
                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Document uploaded successfully.";
                response.DocumentId = newDocumentId;
                response.FileUrl = fileUrl;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
