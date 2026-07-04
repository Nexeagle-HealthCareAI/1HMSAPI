using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetCreditApprovalsResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public List<CreditApprovalItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class CreditApprovalItem
    {
        public Guid CreditApprovalId { get; set; }
        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal RequestedAmount { get; set; }
        public string? PaymentMode { get; set; }
        public decimal ResultingCreditBalance { get; set; }
        public string? Reason { get; set; }
        public string? RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? DecidedAt { get; set; }
        public string? DecidedBy { get; set; }
        public string? DecisionNote { get; set; }
    }
}
