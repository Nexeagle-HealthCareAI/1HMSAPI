using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadPathologyReportTemplateHandler : IRequestHandler<UploadPathologyReportTemplateRequestModel, UploadPathologyReportTemplateResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadPathologyReportTemplateHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PathologyTemplatesContainer"] ?? "pathology-templates";
            _blobStorageService = blobStorageService;
            _context = context;
        }

        public async Task<UploadPathologyReportTemplateResponseModel> Handle(UploadPathologyReportTemplateRequestModel request, CancellationToken cancellationToken)
        {
            UploadPathologyReportTemplateResponseModel result = new();
            try
            {
                var template = await _context.PathologyReportTemplate
                    .FirstOrDefaultAsync(t => t.TemplateId == request.TemplateId && t.HospitalId == request.HospitalId, cancellationToken);
                    
                if (template == null)
                {
                    result.Success = false;
                    result.Message = "Invalid template Id or hospital Id";
                    return result;
                }

                if (request.File == null || request.File.Length == 0)
                {
                    result.Success = false;
                    result.Message = "No file uploaded";
                    return result;
                }

                var url = await _blobStorageService.UploadAsync(request.TemplateId.ToString(), request.File, _containerName, cancellationToken);

                template.HeaderBlobPath = url;
                await _context.SaveChangesAsync(cancellationToken);

                result.Success = true;
                result.Url = url;
                result.Message = "Report template uploaded successfully.";
            }
            catch(Exception ex)
            {
                result.Success = false;
                result.Url = null;
                result.Message = $"An error occurred while uploading the report template: {ex.Message}";
            }

            return result;
        }
    }
}
