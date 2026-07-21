using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteHealthLockerDocumentHandler : IRequestHandler<DeleteHealthLockerDocumentRequestModel, DeleteHealthLockerDocumentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeleteHealthLockerDocumentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<DeleteHealthLockerDocumentResponseModel> Handle(DeleteHealthLockerDocumentRequestModel request, CancellationToken cancellationToken)
        {
            var response = new DeleteHealthLockerDocumentResponseModel();

            // Scoped to (DocumentId, Mobile) together — a guessed DocumentId belonging to another
            // patient simply won't match, same as "not found".
            var document = await _context.PatientHealthLockerDocuments
                .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId && d.Mobile == request.Mobile, cancellationToken);

            if (document == null)
            {
                response.Message = "Document not found.";
                return response;
            }

            var isDeleted = await _blobStorageService.DeleteAsync(request.DocumentId.ToString(), _containerName, cancellationToken);
            if (!isDeleted)
            {
                response.Message = "Failed to delete the file from storage.";
                return response;
            }

            _context.PatientHealthLockerDocuments.Remove(document);
            await _context.SaveChangesAsync(cancellationToken);

            response.Success = true;
            response.Message = "Document deleted successfully.";
            return response;
        }
    }
}
