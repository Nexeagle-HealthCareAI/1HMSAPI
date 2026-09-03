using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class EvaluateExpiryAlertsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int BatchesScanned { get; set; }
        public int AlertsRaised { get; set; }
        public int AlertsSkippedDuplicate { get; set; }
        public int SmsDispatched { get; set; }
    }
}
