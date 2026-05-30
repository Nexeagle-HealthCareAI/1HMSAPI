using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAlertCountsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int ActiveTotal { get; set; }
        public int ActiveInfo { get; set; }
        public int ActiveWarning { get; set; }
        public int ActiveCritical { get; set; }
    }
}
