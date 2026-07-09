using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadPrescriptionDrawingRequestModel : IRequest<UploadPrescriptionDrawingResponseModel>
    {
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public string? FileName { get; set; }
        public string? Label { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public string? PatientId { get; set; }
        [Required]
        public Guid AppointmentId { get; set; }
        [JsonIgnore]
        public string? UserName { get; set; }
    }
}
