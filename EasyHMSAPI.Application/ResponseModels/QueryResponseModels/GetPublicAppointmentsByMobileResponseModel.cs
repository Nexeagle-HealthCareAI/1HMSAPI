using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentsByMobileResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        // Echoes the caller's own (already-verified) mobile back — the frontend's JWT lives in an
        // httpOnly cookie specifically so client JS can never read it directly, so this is the
        // only way the UI (e.g. the profile page) gets to display "logged in as +91 XXXXX".
        public string Mobile { get; set; } = string.Empty;
        public List<PublicAppointmentSummary> Appointments { get; set; } = new();
    }
}
