using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Clinical credentials and licensure for medical staff.
    /// Tracks council registrations, qualification degrees, and life-support certifications.
    /// Drives the automated 60/30/7-day license expiry alert system.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrEmployeeCredentials")]
    public class HrEmployeeCredential
    {
        [Key]
        public Guid HrEmployeeCredentialId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        /// <summary>e.g. "NMC", "Bihar Medical Council", "State Nursing Council"</summary>
        [Required]
        [MaxLength(150)]
        public string CouncilName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string RegistrationNumber { get; set; } = null!;

        /// <summary>e.g. "MBBS", "MD", "MS", "DMLT", "B.Sc Nursing", "B.Pharm"</summary>
        [Required]
        [MaxLength(100)]
        public string QualificationDegree { get; set; } = null!;

        [Required]
        public int DegreeCompletionYear { get; set; }

        /// <summary>
        /// The date until which the council registration is valid.
        /// The license expiry watchdog checks this field at 60d, 30d, and 7d intervals.
        /// </summary>
        [Required]
        public DateOnly LicenseValidUntil { get; set; }

        [MaxLength(500)]
        public string? DocumentScanUrl { get; set; }

        /// <summary>Set by an authorised verifier after manual document check.</summary>
        public bool IsVerified { get; set; } = false;

        public Guid? VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }

        // ─── Life Support Certifications ──────────────────────────────────────
        /// <summary>BLS (Basic Life Support) expiry date.</summary>
        public DateOnly? BlsExpiryDate { get; set; }

        /// <summary>ACLS (Advanced Cardiac Life Support) expiry date.</summary>
        public DateOnly? AclsExpiryDate { get; set; }

        /// <summary>PALS (Pediatric Advanced Life Support) expiry date.</summary>
        public DateOnly? PalsExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ─── Navigation ───────────────────────────────────────────────────────
        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
