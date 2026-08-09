using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Which nurse covers which specific patient for which shift -- a per-patient layer on top of
    /// the ward-level NurseShiftAssignment roster, structurally identical to it. Team model, same as
    /// the ward roster: multiple different nurses can each hold their own ACTIVE row for the same
    /// admission+shift+date at once (a filtered unique index only stops the SAME nurse being
    /// double-assigned). Deliberately independent of NurseShiftAssignment -- a nurse doesn't need to
    /// be on the ward roster to be assigned to a specific patient. ShiftDate NULL means a standing
    /// assignment; a real date means a one-off cover for that IST calendar date.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("PatientNurseAssignment")]
    public class PatientNurseAssignment
    {
        [Key]
        public Guid PatientNurseAssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid NurseUserId { get; set; }
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
