using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeletePrescriptionAttachmentHandler : IRequestHandler<DeletePrescriptionAttachmentRequestModel, DeletePrescriptionAttachmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeletePrescriptionAttachmentHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }

        public async Task<DeletePrescriptionAttachmentResponseModel> Handle(DeletePrescriptionAttachmentRequestModel request, CancellationToken cancellationToken)
        {
            DeletePrescriptionAttachmentResponseModel response = new();
            try
            {
                var existingAttachment = await _context.PrescriptionAttachments
                    .Where(x => x.AttachmentId == request.AttachmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingAttachment is not null)
                {
                    bool isDeleted = await _blobStorageService.DeleteAsync(request.AttachmentId.ToString(), _containerName, cancellationToken);
                    if(isDeleted)
                    {
                        _context.PrescriptionAttachments.Remove(existingAttachment);
                        await _context.SaveChangesAsync(cancellationToken);
                        response.Success = true;
                        response.Message = "Attachment deleted successfully.";
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = "Failed to delete attachment from blob storage.";
                    }
                }
                else
                {
                    response.Success = false;
                    response.Message = "Attachment not found.";
                }
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
