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
    }
}
