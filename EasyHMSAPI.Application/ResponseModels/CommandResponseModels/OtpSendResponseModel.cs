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
        // The code itself, for the sign-up / login page to show. Only ever set on a Development server for a tester number
        // (StaffAuth:TesterMobiles, see TesterAccounts), where nothing is sent on WhatsApp or email. Null everywhere else.
        public string? DevCode { get; set; }
    }
} 