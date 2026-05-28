using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertExpenseResponseModel
    {
        public Guid ExpenseId { get; set; }
        public string? Message { get; set; }
    }
}
