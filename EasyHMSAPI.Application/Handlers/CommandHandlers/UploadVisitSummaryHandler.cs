using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadVisitSummaryHandler : IRequestHandler<UploadVisitSummaryRequestModel, UploadVisitSummaryResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public UploadVisitSummaryHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionsContainer"] ?? string.Empty;
        }

        public async Task<UploadVisitSummaryResponseModel> Handle(UploadVisitSummaryRequestModel request, CancellationToken cancellationToken)
        {
            UploadVisitSummaryResponseModel response = new();
            try
            {
                var existingAppointment  = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingAppointment is not null)
                {
                    var fileUrl = await _blobStorageService.UploadAsync(existingAppointment.ApptId.ToString(), request.File, _containerName, cancellationToken);
                    if(!string.IsNullOrEmpty(fileUrl))
                    {
                        existingAppointment.PdfUrl = fileUrl;
                        await _context.SaveChangesAsync(cancellationToken);

                        response.Success = true;
                        response.Message = "Visit summary uploaded successfully.";
                        response.Url = fileUrl;
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = "Failed to upload visit summary.";
                    }
                }
                else
                {
                    response.Success = false;
                    response.Message = "Appointment not found.";
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
