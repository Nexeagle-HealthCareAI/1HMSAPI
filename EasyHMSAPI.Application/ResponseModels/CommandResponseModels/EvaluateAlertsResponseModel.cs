using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class EvaluateAlertsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int AdmissionsScanned { get; set; }
        public int AlertsRaised { get; set; }
        public int AlertsSkippedDuplicate { get; set; }
        public int EddBreachRaised { get; set; }
        public int DepositLowRaised { get; set; }
        public int ConsentPendingRaised { get; set; }
    }
}
