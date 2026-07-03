using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PlaceClinicalOrderResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OrderId { get; set; }
        public int LineCount { get; set; }
        public int ChargedLineCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DiscontinueClinicalOrderLineResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OrderLineId { get; set; }
        public bool ChargeVoided { get; set; }
    }
}
