using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingCategoryAnalyticsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public BillingCategoryAnalyticsData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BillingCategoryAnalyticsData
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetAmount { get; set; }
        public List<CategoryBreakdownItem> RevenueByCategory { get; set; } = new();
        public List<CategoryBreakdownItem> ExpenseByCategory { get; set; } = new();
        public List<DailyTrendPoint> DailyTrend { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class CategoryBreakdownItem
    {
        public string CategoryCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DailyTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
    }
}
