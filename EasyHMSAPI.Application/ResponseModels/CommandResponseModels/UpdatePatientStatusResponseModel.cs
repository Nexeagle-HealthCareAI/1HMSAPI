namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UpdatePatientStatusResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
    }

    public class StatusHistoryItem
    {
        public string? Status { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
    }
}
