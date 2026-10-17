using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — never a
    // client-supplied field. ApptId is optional: a patient MAY tag an upload to a past
    // appointment of theirs for context, but the handler still verifies ownership if one is given.
    [ExcludeFromCodeCoverage]
    public class UploadHealthLockerDocumentRequestModel : IRequest<UploadHealthLockerDocumentResponseModel>
    {
        [JsonIgnore]
        public string Mobile { get; set; } = string.Empty;
        [Required]
        public IFormFile? File { get; set; }
        [Required]
        public string? FileName { get; set; }
        public string? DocumentType { get; set; }
        public string? Notes { get; set; }
        public Guid? ApptId { get; set; }
        // One of the account's family members; empty = the account holder. Checked against the owner's mobile.
        public Guid? MemberId { get; set; }
        // For a visit to a doctor who is not in our system: who, where, and when. Not allowed together with ApptId.
        public string? ExternalDoctorName { get; set; }
        public string? ExternalClinicName { get; set; }
        public string? ExternalLocation { get; set; }
        public DateTime? VisitDate { get; set; }
    }
}
