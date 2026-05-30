using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RaiseAlertResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AlertId { get; set; }
        public bool SmsSent { get; set; }
        public bool WhatsAppSent { get; set; }
    }
}
