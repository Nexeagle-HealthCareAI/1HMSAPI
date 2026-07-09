using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeletePrescriptionDrawingHandler : IRequestHandler<DeletePrescriptionDrawingRequestModel, DeletePrescriptionDrawingResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeletePrescriptionDrawingHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionDrawingsContainer"] ?? string.Empty;
        }

        public async Task<DeletePrescriptionDrawingResponseModel> Handle(DeletePrescriptionDrawingRequestModel request, CancellationToken cancellationToken)
        {
            DeletePrescriptionDrawingResponseModel response = new();
            try
            {
                var existingDrawing = await _context.PrescriptionDrawings
                    .Where(x => x.DrawingId == request.DrawingId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingDrawing is not null)
                {
                    bool isDeleted = await _blobStorageService.DeleteAsync(request.DrawingId.ToString(), _containerName, cancellationToken);
                    if (isDeleted)
                    {
                        _context.PrescriptionDrawings.Remove(existingDrawing);
                        await _context.SaveChangesAsync(cancellationToken);
                        response.Success = true;
                        response.Message = "Drawing deleted successfully.";
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = "Failed to delete drawing from blob storage.";
                    }
                }
                else
                {
                    response.Success = false;
                    response.Message = "Drawing not found.";
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
