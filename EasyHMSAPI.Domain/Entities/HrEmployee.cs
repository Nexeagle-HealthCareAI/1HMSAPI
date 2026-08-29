using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Central employee master record for the 1HR Suite.
    /// Covers all 4 workforce taxonomy categories:
    ///   1. Full-Time Salaried (Track A – TDS Sec 192, PF, ESI)
    ///   2. Visiting Consultants (Track B – TDS Sec 194J)
    ///   3. Contractual staff
    ///   4. Interns / trainees
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrEmployees")]
    public class HrEmployee
    {
        [Key]
        public Guid HrEmployeeId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        /// <summary>Auto-generated: EMP-YYYY-NNNN (e.g. EMP-2026-0042)</summary>
        [Required]
        [MaxLength(50)]
        public string EmployeeCode { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Gender { get; set; } = null!;

        [Required]
        public DateOnly DateOfBirth { get; set; }

        [MaxLength(10)]
        public string? BloodGroup { get; set; }

        [Required]
        [MaxLength(20)]
        public string ContactNumber { get; set; } = null!;

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? PhotoObjectUrl { get; set; }

        /// <summary>
        /// FULL_TIME_SALARIED | VISITING_CONSULTANT | CONTRACTUAL | INTERN
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EmploymentType { get; set; } = null!;

        public Guid DepartmentId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Designation { get; set; } = null!;

        public Guid? ReportingManagerId { get; set; }

        [Required]
        public DateOnly DateOfJoining { get; set; }

        public DateOnly? ProbationEndDate { get; set; }

        // ─── Statutory ────────────────────────────────────────────────────────
        [Required]
        [MaxLength(20)]
        public string PanNumber { get; set; } = null!;

        /// <summary>Stored as SHA-256 hash — never store raw Aadhaar.</summary>
        [MaxLength(128)]
        public string? AadhaarNumberHash { get; set; }

        /// <summary>Universal Account Number for EPF — only for salaried employees.</summary>
        [MaxLength(30)]
        public string? UanNumber { get; set; }

        /// <summary>ESI Insurance Premium (IP) number — only when gross ≤ ₹21,000/mo.</summary>
        [MaxLength(30)]
        public string? EsiNumber { get; set; }

        // ─── Banking ──────────────────────────────────────────────────────────
        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(20)]
        public string? BankIfsc { get; set; }

        // ─── Payroll Track ────────────────────────────────────────────────────
        /// <summary>TRACK_A_SALARIED | TRACK_B_CONSULTANT</summary>
        [Required]
        [MaxLength(30)]
        public string PayrollTrack { get; set; } = "TRACK_A_SALARIED";

        public bool IsActive { get; set; } = true;

        /// <summary>ACTIVE | INACTIVE | ON_LEAVE | SUSPENDED</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ─── Navigation ───────────────────────────────────────────────────────
        public Hospital Hospital { get; set; } = null!;
        public Department? Department { get; set; }
        public ICollection<HrEmployeeCredential> Credentials { get; set; } = new List<HrEmployeeCredential>();
        public ICollection<HrVaccinationRecord> VaccinationRecords { get; set; } = new List<HrVaccinationRecord>();
        public ICollection<HrNeedleStickLog> NeedleStickLogs { get; set; } = new List<HrNeedleStickLog>();
        public ICollection<HrDutyRoster> DutyRosters { get; set; } = new List<HrDutyRoster>();
        public ICollection<HrAttendanceLog> AttendanceLogs { get; set; } = new List<HrAttendanceLog>();
        public ICollection<HrLeaveRequest> LeaveRequests { get; set; } = new List<HrLeaveRequest>();
        public HrSalaryStructure? SalaryStructure { get; set; }
        public HrConsultantFeeConfig? ConsultantFeeConfig { get; set; }
    }
}
