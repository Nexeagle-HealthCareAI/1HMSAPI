using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadInvoiceSettingsTemplateRequestModel : IRequest<UploadInvoiceSettingsTemplateResponseModel>
    {
        public IFormFile? TemplateFile { get; set; }
        public Guid HospitalId { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
