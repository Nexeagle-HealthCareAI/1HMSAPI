using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Anonymous/bot-facing patient-detail correction — same AppointmentId-as-secret + Mobile gate
    // as the sibling public appointment endpoints. Writes land on the shared PatientRegistration
    // row the appointment's PatientId points at (see handler header comment) — this is patient-
    // record data, not appointment-scoped data, and there's no per-appointment snapshot in the
    // schema to write to instead.
    [ExcludeFromCodeCoverage]
    public class PublicUpdatePatientAppointmentRequestModel : IRequest<PublicUpdatePatientAppointmentResponseModel>
    {
        [JsonIgnore]
        public Guid AppointmentId { get; set; }

        [Required]
        public string Mobile { get; set; } = string.Empty;

        [Required]
        public PublicPatientUpdateFields Patient { get; set; } = new();
    }

    // Every field optional — this is a PATCH, callers send only what they're correcting. At least
    // one must be non-empty (enforced in the handler, not via [Required], since any single one
    // alone is a valid request). Unset fields leave the stored value untouched, same
    // omit-means-no-op idiom AppointmentBookingHelpers.FindOrCreatePatientAsync already uses for
    // these same PatientRegistration columns — so there's no way to blank out a field via this
    // endpoint, only to correct it to a real value.
    [ExcludeFromCodeCoverage]
    public class PublicPatientUpdateFields
    {
        public string? FullName { get; set; }
        public short? Age { get; set; }
        public string? Gender { get; set; }
        public string? Guardian { get; set; }
    }
}
