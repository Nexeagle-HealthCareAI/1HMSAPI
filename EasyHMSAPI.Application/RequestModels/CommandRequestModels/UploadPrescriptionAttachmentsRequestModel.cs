using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadPrescriptionAttachmentsRequestModel : IRequest<UploadPrescriptionAttachmentsResponseModel>
    {
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public string? FileName { get; set; }
        [Required]
        public string? ReportType { get; set; }
        [Required]
        public string? Notes { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        public Guid DoctorId { get; set; }
        [Required]
        public string? PatientId { get; set; }
        [Required]
        public Guid AppointmentId { get; set; }
        // Optional -- lets a caller that already generated the id before this upload (e.g. the
        // prescription-QR flow, which has to embed AttachmentId into the PDF before this row
        // exists) pin the new row to that same id. Falls back to a fresh GUID if omitted, so
        // every other existing caller of this endpoint is unaffected.
        public Guid? AttachmentId { get; set; }
        [JsonIgnore]
        public string? UserName { get; set; }
    }
}
