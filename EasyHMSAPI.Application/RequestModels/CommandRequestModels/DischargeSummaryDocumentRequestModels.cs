using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadDischargeSummaryPdfRequestModel : IRequest<UploadDischargeSummaryPdfResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public IFormFile File { get; set; } = null!;
    }

    // Replaces the public link's token (and restarts its validity), so any copy of the old QR/link stops working.
    [ExcludeFromCodeCoverage]
    public class RegenerateDischargeLinkRequestModel : IRequest<RegenerateDischargeLinkResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public string? Reason { get; set; }
    }

    // Sends the already-uploaded PDF (see UploadDischargeSummaryPdfRequestModel) as a WhatsApp
    // document — same document-header template shape as visit-summary/prescription sends.
    [ExcludeFromCodeCoverage]
    public class SendDischargeSummaryWhatsAppRequestModel : IRequest<SendDischargeSummaryWhatsAppResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        // Optional override — falls back to the patient's registered mobile number when omitted.
        public string? MobileNumber { get; set; }
    }
}
