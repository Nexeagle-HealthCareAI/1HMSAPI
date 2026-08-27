using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PublicUpdatePatientAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PublicAppointmentSummary? Appointment { get; set; }
        public PublicPatientSummary? Patient { get; set; }
    }

    // Post-update snapshot of just the four editable fields, echoed back so the bot can confirm
    // without a second lookup — deliberately excludes every other PatientRegistration column
    // (address, insurance, AbhaId, etc.), same "don't expose more than what's needed" posture as
    // PublicAppointmentSummary.
    [ExcludeFromCodeCoverage]
    public class PublicPatientSummary
    {
        public string FullName { get; set; } = string.Empty;
        public short? Age { get; set; }
        public string? Gender { get; set; }
        public string? Guardian { get; set; }
    }
}
