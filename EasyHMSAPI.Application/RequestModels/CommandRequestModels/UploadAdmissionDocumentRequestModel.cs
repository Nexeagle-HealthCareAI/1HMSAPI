using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadAdmissionDocumentRequestModel : IRequest<UploadAdmissionDocumentResponseModel>
    {
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public Guid AdmissionId { get; set; }
        [JsonIgnore]
        public string? UploadedByUserName { get; set; }
    }
}
