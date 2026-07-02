using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInstrumentSetsResponseModel
    {
        public List<InstrumentSetDataModel> Sets { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class InstrumentSetDataModel
    {
        public Guid InstrumentSetId { get; set; }
        public string SetCode { get; set; } = null!;
        public string SetName { get; set; } = null!;
        public string? Category { get; set; }
        public string CurrentStatus { get; set; } = null!;
        public string? CurrentLocation { get; set; }
        public bool IsActive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSterilizationCycleHistoryResponseModel
    {
        public List<SterilizationCycleDataModel> Cycles { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SterilizationCycleDataModel
    {
        public Guid SterilizationCycleId { get; set; }
        public string CycleNumber { get; set; } = null!;
        public string? AutoclaveLabel { get; set; }
        public string CycleType { get; set; } = null!;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string BiologicalIndicatorResult { get; set; } = null!;
        public string? ChemicalIndicatorResult { get; set; }
        public string OperatorName { get; set; } = null!;
        public List<string> SetCodes { get; set; } = new();
    }
}
