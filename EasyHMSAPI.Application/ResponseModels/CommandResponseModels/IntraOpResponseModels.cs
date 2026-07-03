using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SaveIntraOpRecordResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? IntraOpRecordId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordIntraOpItemUsageResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? IntraOpItemUsageId { get; set; }
        public Guid? ChargeEventId { get; set; }
        public Guid? InventoryMovementId { get; set; }
    }
}
