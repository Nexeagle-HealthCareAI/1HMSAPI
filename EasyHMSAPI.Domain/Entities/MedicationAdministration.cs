using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// MAR — one recorded action (administered/held/refused/patient-not-available) against a
    /// scheduled dose slot of a MEDICATION ClinicalOrderLine. The slot schedule itself is never
    /// persisted (computed on read from Frequency/DurationDays/OrderedAt via
    /// MarScheduleCalculator) — only what the nurse actually did is a row here. MedicationOrderId
    /// is legacy (pre-CPOE dead schema) and always null for rows written by this phase — every
    /// new row populates OrderLineId instead (see CK_MA_ExactlyOneOrderRef).
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("MedicationAdministration")]
    public class MedicationAdministration
    {
        [Key]
        public Guid MedicationAdministrationId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        // Legacy — never set by new code, kept mapped only so EF doesn't choke on the column.
        public Guid? MedicationOrderId { get; set; }

        public Guid? OrderLineId { get; set; }

        public DateTime ScheduledFor { get; set; }

        public string ActionStatus { get; set; } = null!;   // ADMINISTERED / HELD / REFUSED / PATIENT_NOT_AVAILABLE

        public string? AdministeredDose { get; set; }
        public string? AdministeredRoute { get; set; }
        public string? AdministrationSite { get; set; }

        public DateTime ActedAt { get; set; }
        public string? ActedBy { get; set; }
        public Guid? ActedByUserId { get; set; }

        public bool WitnessRequired { get; set; }
        public string? WitnessName { get; set; }
        public Guid? WitnessUserId { get; set; }
        public DateTime? WitnessConfirmedAt { get; set; }

        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
