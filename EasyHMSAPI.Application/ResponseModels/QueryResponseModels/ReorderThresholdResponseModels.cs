using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetReorderThresholdSuggestionsResponseModel
    {
        public List<ReorderThresholdSuggestionRow> Suggestions { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ReorderThresholdSuggestionRow
    {
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        // Trailing-consumption inputs, shown so the manager can judge the suggestion, not just accept blind.
        public decimal Trailing4WeekIssuedQty { get; set; }
        public decimal WeeklyAverageConsumption { get; set; }
        public decimal CurrentMinStockLevel { get; set; }
        public decimal? CurrentMaxStockLevel { get; set; }
        public decimal SuggestedMinStockLevel { get; set; }
        public decimal SuggestedMaxStockLevel { get; set; }
        // True when current CurrentStock is already below the suggested Min — the item this report
        // exists to surface first.
        public bool IsBelowSuggestedMin { get; set; }
    }
}
