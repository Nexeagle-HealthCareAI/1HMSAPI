using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AddPaymentEventResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AddPaymentData? Data { get; set; }
        // True when this request would have left the patient with a credit balance and was
        // held for admin approval instead of being posted immediately — no BillingPayment was created.
        public bool PendingApproval { get; set; }
        public Guid? CreditApprovalId { get; set; }
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
