using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ReceiveBloodBagResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? BloodBagId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReserveBloodBagResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DiscardBloodBagResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordTransfusionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? TransfusionEventId { get; set; }
        public Guid? ChargeEventId { get; set; }
    }
}
