using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Live (Item, Store) stock position — what the board/pickers read for "how much is in this
    /// store," separate from InventoryItem.CurrentStock's hospital-wide total. Not trigger-maintained
    /// — updated by the same handler that inserts the InventoryMovement.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class StockLevel
    {
        [Key]
        public Guid StockLevelId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid StoreId { get; set; }

        public decimal QtyOnHand { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
