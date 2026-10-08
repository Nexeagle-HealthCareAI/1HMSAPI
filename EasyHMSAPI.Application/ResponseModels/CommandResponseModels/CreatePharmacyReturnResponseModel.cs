using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePharmacyReturnResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid ReturnId { get; set; }
        public string? ReturnNo { get; set; }
        // Value of the goods returned (what the invoice is reduced by).
        public decimal TotalRefundAmount { get; set; }
        // Money actually paid back: only what the customer had paid above the reduced bill.
        public decimal CashRefundAmount { get; set; }
    }
}
