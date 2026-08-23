using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AddChargeEventResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AddChargesData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AddChargesData
    {
        public Guid EncounterId { get; set; }
        public int ChargeCount { get; set; }
        public decimal TotalGross { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalIncentive { get; set; }

        // GST totals
        public decimal TotalTaxable { get; set; }
        public decimal TotalCgst { get; set; }
        public decimal TotalSgst { get; set; }
        public decimal TotalIgst { get; set; }
        public decimal TotalTax { get; set; }

        public List<ChargeEventDetail>? ChargeEvents { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ChargeEventDetail
    {
        public Guid ChargeEventId { get; set; }
        public string? DisplayName { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public DateTime ServiceDate { get; set; }
        public bool IsBackdated { get; set; }

        // GST snapshot for the line
        public string? HsnSacCode { get; set; }
        public decimal? GstRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsTaxInclusive { get; set; }
        public bool IsInterState { get; set; }

        // Discount approval (populated when posted discount exceeded the effective cap)
        public Guid? DiscountApprovalId { get; set; }
        public bool DiscountApprovalRequired { get; set; }
        public decimal? DiscountCapPercent { get; set; }
    }
}
