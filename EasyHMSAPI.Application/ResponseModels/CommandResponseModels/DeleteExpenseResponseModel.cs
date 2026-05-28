using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteExpenseResponseModel
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
    }
}
