using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HospitalRegisterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? HospitalUserId { get; set; }
        // Soft feedback about a referral code entered at registration -- an invalid/expired/
        // already-used code, or CMS being unreachable, never blocks registration; this just tells
        // the frontend whether to show a success or "not recognized" message.
        public bool ReferralCodeApplied { get; set; }
        public string? ReferralCodeMessage { get; set; }
    }
}