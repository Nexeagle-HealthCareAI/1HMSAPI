using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Anyone who writes or reviews a Health Wiki article. Mobile and email are never exposed publicly.
    [ExcludeFromCodeCoverage]
    [Table("HealthWikiContributor")]
    public class HealthWikiContributor
    {
        public const string TypeHospitalDoctor = "HOSPITAL_DOCTOR";
        public const string TypeIndependentDoctor = "INDEPENDENT_DOCTOR";
        public const string TypeHealthWorker = "HEALTH_WORKER";
        public const string TypeWriter = "WRITER";
        public const string TypeStaff = "STAFF";

        public const string StatusInvited = "INVITED";
        public const string StatusPending = "PENDING";
        public const string StatusVerified = "VERIFIED";
        public const string StatusRejected = "REJECTED";

        [Key]
        public Guid ContributorId { get; set; }
        public string Type { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Bio { get; set; }
        public string? Speciality { get; set; }
        public string? Qualification { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? RoleTitle { get; set; }
        public string? Organisation { get; set; }
        public string? FieldOfWork { get; set; }
        public string Status { get; set; } = StatusPending;
        public string? RejectReason { get; set; }
        public string EnrolmentSource { get; set; } = "SELF_ENROLLED";
        public Guid? DoctorId { get; set; }
        public DateTime? ConsentAt { get; set; }
        public string? ConsentVersion { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? LinkSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
