using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class BulkAddExpenseResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int CreatedCount { get; set; }
        public List<Guid> ExpenseIds { get; set; } = new();
    }
}
