using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Roster backbone for the Nursing Station feature -- which nurse covers which ward for which
    /// shift. Ward-level grain (not per-admission) and team-based: multiple different nurses can
    /// each hold their own ACTIVE row for the same ward+shift+date at once (a filtered unique index
    /// only stops the SAME nurse being double-booked). ShiftDate NULL means a standing assignment;
    /// a real date means a one-off cover for that IST calendar date. No dedicated Nurse table
    /// exists (unlike Doctor) -- NurseUserId points straight at Users.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("NurseShiftAssignment")]
    public class NurseShiftAssignment
    {
        [Key]
        public Guid NurseShiftAssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid NurseUserId { get; set; }
        public string WardCode { get; set; } = string.Empty;
        public string ShiftCode { get; set; } = string.Empty;   // MORNING / EVENING / NIGHT
        public DateTime? ShiftDate { get; set; }                // NULL = standing assignment

        public string StatusCode { get; set; } = "ACTIVE";      // ACTIVE / RELEASED

        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? UnassignedAt { get; set; }
        public string? UnassignedBy { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
