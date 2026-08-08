using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Adds several expense line items under one shared category/date/vendor/payment-mode/status
    // in a single call (e.g. logging today's FOOD spend as separate lunch/tea/dinner lines) --
    // each line gets its own Amount and Reason, everything else is shared across the batch.
    [ExcludeFromCodeCoverage]
    public class BulkAddExpenseRequestModel : IRequest<BulkAddExpenseResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string CategoryCode { get; set; } = null!;
        public DateTime? ExpenseDate { get; set; }
        public string? Vendor { get; set; }
        public string? PaymentMode { get; set; }
        public string? StatusCode { get; set; }
        public List<BulkExpenseLine> Lines { get; set; } = new();

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BulkExpenseLine
    {
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
    }
}
