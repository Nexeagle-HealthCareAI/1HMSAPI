using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetNarcoticRegisterResponseModel
    {
        public List<NarcoticRegisterEntryDataModel> Entries { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class NarcoticRegisterEntryDataModel
    {
        public Guid RegisterEntryId { get; set; }
        public string ItemName { get; set; } = null!;
        public string? BatchNumber { get; set; }
        public string? StoreName { get; set; }
        public string FormType { get; set; } = null!;
        public string Direction { get; set; } = null!;
        public decimal Qty { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? PatientId { get; set; }
        public string? PrescriberRef { get; set; }
        public string? IssuedBy { get; set; }
        public string WitnessBy { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetColdChainReadingsResponseModel
    {
        public List<ColdChainReadingDataModel> Readings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ColdChainReadingDataModel
    {
        public Guid LogId { get; set; }
        public Guid StoreId { get; set; }
        public string? StoreName { get; set; }
        public DateTime RecordedAt { get; set; }
        public decimal TempCelsius { get; set; }
        public string? RecordedBy { get; set; }
        public bool BreachFlag { get; set; }
    }
}
