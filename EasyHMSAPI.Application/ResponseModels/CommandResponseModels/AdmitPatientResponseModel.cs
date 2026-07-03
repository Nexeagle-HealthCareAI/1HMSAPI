using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AdmitPatientResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? AdmissionNo { get; set; }
        public string? PatientId { get; set; }
        public bool IsNewPatient { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public bool WasExisting { get; set; }
        // ADMITTED, or PRE_ADMIT when IsPreRegistration was set — lets the frontend show the right
        // success state ("pre-registered, confirm arrival later" vs. a completed admission).
        public string? StatusCode { get; set; }

        // IPD billing encounter opened for this admission (null when EnableIpdBilling=false).
        public Guid? EncounterId { get; set; }
        public string? PayerType { get; set; }
        // Bed assigned at admit (null when none chosen).
        public Guid? BedId { get; set; }
        public Guid? BedAssignmentId { get; set; }
    }
}
