using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDischargeTpaSplitResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PayerType { get; set; }
        public decimal PayableTotal { get; set; }
        public decimal NonPayableTotal { get; set; }
        public decimal UnclassifiedTotal { get; set; }
        public List<TpaSplitLineModel> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class TpaSplitLineModel
    {
        public string? DisplayName { get; set; }
        public string? CategoryCode { get; set; }
        public decimal NetAmount { get; set; }
        // null = unclassified (no ChargeId link, or the linked ChargeMaster row is gone).
        public bool? IsIRDAIPayable { get; set; }
    }
}
