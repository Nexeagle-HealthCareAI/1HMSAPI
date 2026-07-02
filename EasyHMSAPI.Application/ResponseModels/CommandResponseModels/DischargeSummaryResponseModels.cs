using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SaveDischargeSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DischargeSummaryId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SignDischargeSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
