using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>CurrentStock is not trigger-maintained — every handler that inserts an InventoryMovement must update it in the same transaction.</summary>
    [ExcludeFromCodeCoverage]
    [Table("InventoryItem")]
    public class InventoryItem
    {
        [Key]
        public Guid InventoryItemId { get; set; }
        public Guid HospitalId { get; set; }

        public string ItemCode { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public string? GenericName { get; set; }
        public string? Manufacturer { get; set; }

        public string Category { get; set; } = null!;   // CONSUMABLE/DRUG/DISPOSABLE/SURGICAL/IMPLANT/OTHER
        public string Unit { get; set; } = null!;

        public decimal? DefaultRate { get; set; }
        public string? HsnSacCode { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool IsTaxable { get; set; }

        public Guid? ChargeId { get; set; }

        public decimal CurrentStock { get; set; }
        public decimal MinStockLevel { get; set; }
        public string? StoreLocation { get; set; }

        public string? ScheduleClass { get; set; }   // H/H1/X/NARCOTIC — null means unregulated/OTC
        public bool IsLasa { get; set; }
        public bool IsHighAlert { get; set; }
        public string? StorageCondition { get; set; }   // ROOM/COLD_CHAIN/FROZEN/CONTROLLED
        public decimal ReorderQty { get; set; }
        public decimal? MaxStockLevel { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
