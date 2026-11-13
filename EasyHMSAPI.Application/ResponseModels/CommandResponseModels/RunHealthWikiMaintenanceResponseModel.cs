using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RunHealthWikiMaintenanceResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public int RemindersSent { get; set; }
        public int RemindersNotSent { get; set; }
        public int TopicsClosed { get; set; }
        public int OtpsRemoved { get; set; }
        public int SessionsRemoved { get; set; }
    }
}
