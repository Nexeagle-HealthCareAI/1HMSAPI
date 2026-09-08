using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPharmacySalesTrendResponseModel
    {
        public List<SalesTrendPoint> Points { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SalesTrendPoint
    {
        public string PeriodLabel { get; set; } = null!;
        public DateTime PeriodStart { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalQty { get; set; }
        public int LineCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyAbcAnalysisResponseModel
    {
        public List<AbcAnalysisRow> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AbcAnalysisRow
    {
        public Guid? InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public decimal TotalValue { get; set; }
        public decimal TotalQty { get; set; }
        public decimal CumulativePercent { get; set; }
        public string Class { get; set; } = null!;   // A/B/C
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyGstLiabilityResponseModel
    {
        public List<GstLiabilityRow> Rows { get; set; } = new();
        public decimal GrandTotalTax { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GstLiabilityRow
    {
        public string? HsnSacCode { get; set; }
        public decimal? GstRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalSales { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyExpiryLossPreventedResponseModel
    {
        public decimal RecoveredValue { get; set; }     // value returned to vendors in the window (loss avoided)
        public decimal AtRiskValue { get; set; }         // current Orange+Red bucket stock value, as of now
        public int AtRiskBatchCount { get; set; }
        public int RtvNoteCount { get; set; }
    }
}
