using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Leave application record.
    /// PENDING → APPROVED (deducts from HrLeaveBalance) or REJECTED.
    /// Maternity: does NOT deduct from CL/SL — governed by Maternity Benefit (Amendment) Act.
    /// CME: does NOT deduct from CL/SL — dedicated clinical conference quota.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrLeaveRequests")]
    public class HrLeaveRequest
    {
        [Key]
        public Guid HrLeaveRequestId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        /// <summary>CASUAL | SICK | EARNED | MATERNITY | COMP_OFF | CME</summary>
        [Required]
        [MaxLength(30)]
        public string LeaveType { get; set; } = null!;

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(4,1)")]
        public decimal TotalDays { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = null!;

        /// <summary>PENDING | APPROVED | REJECTED | CANCELLED</summary>
        [MaxLength(30)]
        public string Status { get; set; } = "PENDING";

        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [MaxLength(500)]
        public string? MedicalCertificateUrl { get; set; }

        [MaxLength(300)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
