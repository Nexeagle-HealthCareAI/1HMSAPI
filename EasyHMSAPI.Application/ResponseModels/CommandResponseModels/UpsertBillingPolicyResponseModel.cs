using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertBillingPolicyResponseModel
    {
        public Guid BillingPolicyId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public NumberSeriesUpsertResponse? NumberSeries { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NumberSeriesUpsertResponse
    {
        public NumberSeriesItemResponse? Invoice { get; set; }
        public NumberSeriesItemResponse? Receipt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NumberSeriesItemResponse
    {
        public string? SeriesCode { get; set; }
    }
}
