using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetQueueTokenStatusResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? TokenNo { get; set; }
        public string? Status { get; set; }
        public int? CurrentServingTokenNo { get; set; }
        // How many WAITING/CALLED patients (including this one) are queued at or before this
        // token's position -- 1 means "you're up now or next".
        public int? PositionInQueue { get; set; }
        // Documented estimate (PositionInQueue * AppConstants.QueueAverageConsultMinutes), not a promise.
        public int? EstimatedWaitMinutes { get; set; }
    }
}
