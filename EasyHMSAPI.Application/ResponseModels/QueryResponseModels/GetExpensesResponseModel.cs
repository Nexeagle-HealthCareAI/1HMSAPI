using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetExpensesResponseModel
    {
        public List<ExpenseItemModel> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        // Summary across the filtered set (not just the page).
        public decimal TotalAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public int CategoryCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ExpenseItemModel
    {
        public Guid ExpenseId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string? Vendor { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMode { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string? ReferenceNo { get; set; }
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
