using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHealthLockerDocumentsHandler : IRequestHandler<GetHealthLockerDocumentsRequestModel, GetHealthLockerDocumentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public GetHealthLockerDocumentsHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<GetHealthLockerDocumentsResponseModel> Handle(GetHealthLockerDocumentsRequestModel request, CancellationToken cancellationToken)
        {
            var documents = await _context.PatientHealthLockerDocuments
                .Where(d => d.Mobile == request.Mobile)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new HealthLockerDocumentItem
                {
                    DocumentId = d.DocumentId,
                    ApptId = d.ApptId,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    StorageUrl = d.StorageUrl,
                    Notes = d.Notes,
                    UploadedAt = d.UploadedAt,
                })
                .ToListAsync(cancellationToken);

            // Re-sign each URL from its stored object key — S3/MinIO presigned URLs expire within
            // 7 days (see docs/STORAGE-MINIO.md). Unlike PrescriptionAttachments, "healthlocker"
            // isn't one of S3StorageService's specially-named containers, so it falls into that
            // service's default "one object per entity" naming: "{entityId}_{containerName}" —
            // NOT the bare "{entityId}_" prefix those special containers use.
            foreach (var doc in documents)
            {
                doc.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                    _containerName,
                    $"{doc.DocumentId}_{_containerName}",
                    doc.StorageUrl,
                    cancellationToken);
            }

            return new GetHealthLockerDocumentsResponseModel
            {
                Success = true,
                Message = documents.Count == 0 ? "No documents in your health locker yet." : "Documents retrieved successfully.",
                Documents = documents,
            };
        }
    }
}
