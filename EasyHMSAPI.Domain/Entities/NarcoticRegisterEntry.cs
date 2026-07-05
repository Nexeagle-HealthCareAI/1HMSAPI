using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Insert-only NDPS narcotics register. One row per movement of a NARCOTIC-scheduled item;
    /// FormType (3D/3E/3H) records which statutory register the row counts toward — see
    /// NarcoticRegisterHelper for the derivation rule. Immutable, no RowVersion.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class NarcoticRegisterEntry
    {
        [Key]
        public Guid RegisterEntryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid BatchId { get; set; }
        public Guid StoreId { get; set; }

        public string FormType { get; set; } = null!;   // 3D/3E/3H
        public string Direction { get; set; } = null!;  // IN/OUT
        public decimal Qty { get; set; }
        public decimal BalanceAfter { get; set; }

        public string? PatientId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PrescriberRef { get; set; }

        public string? IssuedBy { get; set; }
        public Guid? IssuedByUserId { get; set; }
        public string WitnessBy { get; set; } = null!;
        public Guid? WitnessByUserId { get; set; }

        public DateTime RecordedAt { get; set; }
    }
}
