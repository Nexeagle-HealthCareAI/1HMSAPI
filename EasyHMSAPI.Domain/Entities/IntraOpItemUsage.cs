using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Items actually used during surgery — descriptive (recorded after use), distinct from CPOE's
    /// prescriptive ClinicalOrder. A saved line optionally drives an InventoryMovement/stock
    /// deduction and a billing charge event; Category=IMPLANT rows (with LotNumber/SerialNumber)
    /// double as CSSD's implant traceability log.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("IntraOpItemUsage")]
    public class IntraOpItemUsage
    {
        [Key]
        public Guid IntraOpItemUsageId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }

        public Guid? InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Category { get; set; } = null!;   // CONSUMABLE/IMPLANT

        public decimal Qty { get; set; }
        public string? LotNumber { get; set; }
        public string? SerialNumber { get; set; }

        public Guid? ChargeId { get; set; }
        public decimal? UnitRate { get; set; }
        public Guid? ChargeEventId { get; set; }
        public Guid? InventoryMovementId { get; set; }

        public string RecordedBy { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }
}
