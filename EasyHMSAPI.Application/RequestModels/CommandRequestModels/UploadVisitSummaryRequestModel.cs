using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadVisitSummaryRequestModel : IRequest<UploadVisitSummaryResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
