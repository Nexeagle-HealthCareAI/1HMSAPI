using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadDischargeTemplateRequestModel : IRequest<UploadDischargeTemplateResponseModel>
    {
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
