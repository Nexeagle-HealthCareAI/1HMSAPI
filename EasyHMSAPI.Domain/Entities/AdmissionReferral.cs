using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class AdmissionReferral
    {
        public Guid ReferralId { get; set; }
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        public Guid ReferringDoctorId { get; set; }
        public Guid? AppointmentId { get; set; }
        public Guid? OtPlanId { get; set; }
        public string? ProcedureName { get; set; }
        public DateTime? ProbableAdmissionDate { get; set; }
        public string CaseType { get; set; } = null!;   // EMERGENCY / PLANNED / URGENT
        public string? Notes { get; set; }
        public string StatusCode { get; set; } = "PENDING";   // PENDING / CONVERTED / NOT_ADMITTED / FOLLOW_UP
        public string? NotAdmittedReason { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
        public Guid? ConvertedAdmissionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
