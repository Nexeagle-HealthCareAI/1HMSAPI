using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AddPaymentEventResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AddPaymentData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AddPaymentData
    {
        public Guid PaymentId { get; set; }
        public string? ReceiptNo { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal? CreditAmount { get; set; }
    }
}
