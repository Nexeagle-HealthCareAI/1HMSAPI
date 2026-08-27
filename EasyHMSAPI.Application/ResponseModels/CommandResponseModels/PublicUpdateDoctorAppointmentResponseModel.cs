using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    // Reuses PublicAppointmentSummary (the GET /public/appointments/{id} shape) instead of a
    // hand-picked field subset, so the WhatsApp bot's existing appointment-rendering code can
    // render this confirmation the same way it rendered the GET response shown right before
    // offering "cancel / update / book another". Safe to include Token here (unlike GET) because
    // this endpoint already requires the Mobile match GET doesn't.
    [ExcludeFromCodeCoverage]
    public class PublicUpdateDoctorAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PublicAppointmentSummary? Appointment { get; set; }
        public TokenInfo? Token { get; set; }
    }
}
