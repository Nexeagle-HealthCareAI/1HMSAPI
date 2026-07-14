using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAdmissionDocumentsHandler : IRequestHandler<GetAdmissionDocumentsRequestModel, GetAdmissionDocumentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public GetAdmissionDocumentsHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:AdmissionDocumentsContainer"] ?? string.Empty;
        }

        public async Task<GetAdmissionDocumentsResponseModel> Handle(GetAdmissionDocumentsRequestModel request, CancellationToken cancellationToken)
        {
            GetAdmissionDocumentsResponseModel response = new()
            {
                Success = false,
            };
            try
            {
                var documents = await _context.AdmissionDocument
                    .Where(d => d.AdmissionId == request.AdmissionId && d.HospitalId == request.HospitalId)
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new AdmissionDocumentItem
                    {
                        DocumentId = d.DocumentId,
                        DocumentName = d.DocumentName,
                        ContentType = d.ContentType,
                        FileSizeBytes = d.FileSizeBytes,
                        StorageUrl = d.StorageUrl,
                        UploadedAt = d.UploadedAt,
                        UploadedBy = d.UploadedBy,
                    })
                    .ToListAsync(cancellationToken);

                // Re-sign each URL from its stored object key so links never go stale
                // (S3/MinIO presigned URLs expire within 7 days).
                foreach (var document in documents)
                {
                    document.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                        _containerName,
                        $"{document.DocumentId}_",
                        document.StorageUrl,
                        cancellationToken);
                }

                response.Success = true;
                response.Message = documents.Count == 0 ? "No documents found for this admission." : "Documents retrieved successfully.";
                response.DocumentCount = documents.Count;
                response.Documents = documents;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
