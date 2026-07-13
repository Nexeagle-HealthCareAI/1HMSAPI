using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteAdmissionDocumentHandler : IRequestHandler<DeleteAdmissionDocumentRequestModel, DeleteAdmissionDocumentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeleteAdmissionDocumentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:AdmissionDocumentsContainer"] ?? string.Empty;
        }

        public async Task<DeleteAdmissionDocumentResponseModel> Handle(DeleteAdmissionDocumentRequestModel request, CancellationToken cancellationToken)
        {
            DeleteAdmissionDocumentResponseModel response = new();
            try
            {
                var existingDocument = await _context.AdmissionDocument
                    .Where(d => d.DocumentId == request.DocumentId && d.AdmissionId == request.AdmissionId && d.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingDocument is null)
                {
                    response.Success = false;
                    response.Message = "Document not found.";
                    return response;
                }

                var isDeleted = await _blobStorageService.DeleteAsync(existingDocument.DocumentId.ToString(), _containerName, cancellationToken);
                if (!isDeleted)
                {
                    response.Success = false;
                    response.Message = "Failed to delete document from storage.";
                    return response;
                }

                _context.AdmissionDocument.Remove(existingDocument);
                await _context.SaveChangesAsync(cancellationToken);
                response.Success = true;
                response.Message = "Document deleted successfully.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
