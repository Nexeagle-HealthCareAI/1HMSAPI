using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateInventoryItemResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? InventoryItemId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordInventoryMovementResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? InventoryMovementId { get; set; }
        public decimal? NewCurrentStock { get; set; }
        // Echoes back which batch the movement actually posted against — the caller's own pick if
        // BatchId was supplied, or the FEFO-resolved one when only StoreId was.
        public Guid? BatchId { get; set; }
    }
}
