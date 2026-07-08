using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateIndentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid IndentId { get; set; }
        public string? IndentNumber { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ApproveIndentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConvertIndentToPoResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? PoNumber { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class IssueIndentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
