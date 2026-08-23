using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ResolveCheckInResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Set only on a single-match success -- the caller didn't know this ahead of time, unlike
        // IssueQueueTokenResponseModel's caller, which already supplied it.
        public Guid? AppointmentId { get; set; }
        public int? TokenNo { get; set; }
        public string? Status { get; set; }
        // Set only when more than one appointment matched today -- the caller (the WhatsApp bot)
        // disambiguates with the patient, then calls the existing POST public/tokens for the chosen one.
        public List<CheckInCandidate>? Candidates { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CheckInCandidate
    {
        public Guid AppointmentId { get; set; }
        public string? DoctorName { get; set; }
        public DateTime? StartAt { get; set; }
    }
}
