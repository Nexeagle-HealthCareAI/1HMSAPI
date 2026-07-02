using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// WHO 2009 Surgical Safety Checklist — one row per SurgeryCase, 3 phases (Sign-In/Time-Out/
    /// Sign-Out). Item answers are a JSON blob per phase against a fixed item list
    /// (IpdConstants.WhoChecklistItems) — not DB-enforced, soft validation only.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("SurgicalSafetyChecklist")]
    public class SurgicalSafetyChecklist
    {
        [Key]
        public Guid ChecklistId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }

        public DateTime? SignInCompletedAt { get; set; }
        public string? SignInCompletedBy { get; set; }
        public string? SignInItemsJson { get; set; }
        public string? SignInNotes { get; set; }

        public DateTime? TimeOutCompletedAt { get; set; }
        public string? TimeOutCompletedBy { get; set; }
        public string? TimeOutItemsJson { get; set; }
        public string? TimeOutNotes { get; set; }

        public DateTime? SignOutCompletedAt { get; set; }
        public string? SignOutCompletedBy { get; set; }
        public string? SignOutItemsJson { get; set; }
        public string? SignOutNotes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
