using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmProfileResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Authenticated session handle — pass to the profile-update endpoints while it's still
        // live (~20 min). Not a secret itself; the real ABDM X-Token stays server-side.
        public string? TxnId { get; set; }
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        // One-time proof that this profile was verified with ABDM by the caller for the hospital. accounts/link needs it and takes the
        // demographics from what ABDM returned, not from the client. Expires in 15 minutes.
        public string? LinkToken { get; set; }
        // Only populated when this came from GetAbdmProfileHandler (§9 Get Profile) — base64 JPEG.
        public string? ProfilePhoto { get; set; }
    }
}
