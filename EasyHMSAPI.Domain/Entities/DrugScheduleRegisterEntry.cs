using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Insert-only statutory register for regulated-but-non-narcotic drug schedules (Schedule H1
    /// today; ScheduleClass keeps it open for H/X later without a new table). One row per dispense
    /// of a ScheduleClass=H1 item, written alongside the InventoryMovement in the same transaction —
    /// see InventoryCommandHandlers. Narcotics use the separate NarcoticRegisterEntry (mandatory
    /// witness, 3D/3E/3H form tracking) since NDPS rules differ from the Drugs & Cosmetics Rules
    /// H1 register (date/patient/prescriber/qty only, no witness).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DrugScheduleRegisterEntry
    {
        [Key]
        public Guid RegisterEntryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid BatchId { get; set; }
        public Guid StoreId { get; set; }

        public string ScheduleClass { get; set; } = null!;   // H/H1/X
        public decimal Qty { get; set; }

        public string? PatientId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PrescriberRef { get; set; }

        public string? DispensedBy { get; set; }
        public Guid? DispensedByUserId { get; set; }

        public DateTime RecordedAt { get; set; }
    }
}
