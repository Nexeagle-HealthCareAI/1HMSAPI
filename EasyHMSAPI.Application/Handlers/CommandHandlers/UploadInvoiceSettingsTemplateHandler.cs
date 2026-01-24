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
    public class UploadInvoiceSettingsTemplateHandler : IRequestHandler<UploadInvoiceSettingsTemplateRequestModel, UploadInvoiceSettingsTemplateResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadInvoiceSettingsTemplateHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:InvoiceTemplatesContainer"] ?? string.Empty;
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<UploadInvoiceSettingsTemplateResponseModel> Handle(UploadInvoiceSettingsTemplateRequestModel request, CancellationToken cancellationToken)
        {
            UploadInvoiceSettingsTemplateResponseModel response = new();
            try
            {
                var validHospital = await _context.Hospitals.AnyAsync(x => x.HospitalID == request.HospitalId, cancellationToken);
                if(validHospital)
                {
                    var url = await _blobStorageService.UploadAsync(request.HospitalId.ToString(), request.TemplateFile, _containerName, cancellationToken);
                    var existingSetting = await _context.InvoicePrintSettings
                       .Where(x => x.HospitalId == request.HospitalId)
                       .FirstOrDefaultAsync(cancellationToken);
                    if (existingSetting is not null)
                    {
                        existingSetting.URI = url;
                        existingSetting.UpdatedAt = DateTime.UtcNow;                        
                    }
                    else
                    {
                        var invoiceSetting = new InvoicePrintSettings
                        {
                            InvoicePrintId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            URI = url,
                            CreatedByUserId = request.LoggedInUserId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.InvoicePrintSettings.Add(invoiceSetting);
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    response.Success = true;
                    response.Message = "Invoice Print Settings Template uploaded successfully.";
                }
                else
                {
                    response.Success = false;
                    response.Message = "Invalid HospitalId.";
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
