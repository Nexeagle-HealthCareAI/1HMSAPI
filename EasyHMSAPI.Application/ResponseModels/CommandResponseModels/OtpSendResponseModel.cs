using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class OtpSendResponseModel
    {
        public bool Success { get; set; }
        public bool IsSmsSent { get; set; }
        public bool IsEmailSent { get; set; }
        public bool IsWhatsappSent { get; set; }
        public string? Message { get; set; }
        public Guid? UserId { get; set; }
    }
} 