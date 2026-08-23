using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingAiInsightsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public BillingAiInsightsData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BillingAiInsightsData
    {
        // All figures below are computed deterministically from historical billing data (see
        // BillingTrendCalculator) -- Groq narrates them, it never invents the numbers.
        public decimal PredictedNext30DayRevenue { get; set; }
        public decimal PredictedNext30DayExpense { get; set; }
        public decimal PredictedNext30DayNet { get; set; }
        public decimal Avg7DayRevenue { get; set; }
        public decimal Avg30DayRevenue { get; set; }
        public decimal MonthOverMonthRevenueChangePercent { get; set; }
        public decimal MonthOverMonthExpenseChangePercent { get; set; }
        public string Outlook { get; set; } = string.Empty;
        public List<CategoryTrendItem> CategoryTrends { get; set; } = new();
        public List<string> Insights { get; set; } = new();
        public List<AiTrendPoint> HistoricalTrend { get; set; } = new();
        public List<AiTrendPoint> ProjectedTrend { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class CategoryTrendItem
    {
        public string CategoryCode { get; set; } = string.Empty;
        public decimal ChangePercent { get; set; }
        public bool IsLeak { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AiTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
    }
}
