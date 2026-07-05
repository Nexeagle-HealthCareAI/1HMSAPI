using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertEquipmentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid EquipmentId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordMaintenanceLogResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? MaintenanceLogId { get; set; }
        public DateTime? NewNextDueAt { get; set; }
    }
}
