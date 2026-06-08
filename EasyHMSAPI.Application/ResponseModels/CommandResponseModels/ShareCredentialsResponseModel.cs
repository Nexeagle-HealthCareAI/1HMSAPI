using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ShareCredentialsResponseModel
    {
        public bool Success { get; set; }
        // null = channel not requested; true/false = delivery result for a requested channel.
        public bool? WhatsAppSent { get; set; }
        public bool? EmailSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
