using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateInstrumentSetResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? InstrumentSetId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordInstrumentSetMovementResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? NewStatus { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordSterilizationCycleResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? SterilizationCycleId { get; set; }
    }
}
