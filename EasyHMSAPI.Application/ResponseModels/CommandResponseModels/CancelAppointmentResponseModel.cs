using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CancelAppointmentResponseModel
    {
        public string? FinalStatus { get; set; }
        public bool? IsReminderSent { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
