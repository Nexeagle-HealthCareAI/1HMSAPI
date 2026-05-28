using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingPolicyResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public BillingPolicyDataModel? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BillingPolicyDataModel
    {
        public Guid BillingPolicyId { get; set; }
        public Guid HospitalId { get; set; }
        public bool RequirePostBeforeInvoice { get; set; }
        public decimal MaxAutoDiscountPercent { get; set; }
        public string? LabPathTrigger { get; set; }
        public string? LabRadTrigger { get; set; }
        public string? PharmacyIpdTrigger { get; set; }
        public string? OpdConsultTrigger { get; set; }
        public string? IpdBedChargeMode { get; set; }

        // GST
        public string? SupplierGstin { get; set; }
        public string? PlaceOfSupplyStateCode { get; set; }
        public bool DefaultPriceIsTaxInclusive { get; set; }
        public string? TaxRoundingMode { get; set; }

        public Dictionary<string, NumberSeriesResponseModel> NumberSeries { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NumberSeriesResponseModel
    {
        public string? SeriesCode { get; set; }
        public string? Prefix { get; set; }
        public string? YearFormat { get; set; }
        public string? Separator { get; set; }
        public long CurrentValue { get; set; }
        public int PadLength { get; set; }
        public bool IsActive { get; set; }
    }
}
