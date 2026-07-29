using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// ABHA V3 flows for M1: Aadhaar-OTP creation (with optional mobile re-verification and ABHA
    /// address selection) and Mobile/Aadhaar-OTP login for an existing ABHA. Biometric auth, Child
    /// ABHA, and Benefit APIs are out of scope. Every PII field (Aadhaar/mobile/OTP) is encrypted via
    /// <see cref="IAbdmEncryptionService"/> before it's sent. ABDM's X-Token is never exposed to
    /// callers — it's cached server-side, keyed by TxnId.
    /// </summary>
    public interface IAbdmAbhaService
    {
        Task<AbdmOtpTxnResult> GenerateAadhaarOtpAsync(string aadhaarNumber, CancellationToken cancellationToken);

        Task<AbdmEnrollResult> VerifyAadhaarOtpAsync(string txnId, string otp, CancellationToken cancellationToken);

        Task<AbdmOtpTxnResult> GenerateMobileOtpAsync(string txnId, string mobile, CancellationToken cancellationToken);

        Task<AbdmEnrollResult> VerifyMobileOtpAsync(string txnId, string otp, CancellationToken cancellationToken);

        Task<AbdmAddressSuggestionsResult> GetAbhaAddressSuggestionsAsync(string txnId, CancellationToken cancellationToken);

        Task<AbdmEnrollResult> CreateAbhaAddressAsync(string txnId, string abhaAddress, CancellationToken cancellationToken);

        /// <param name="loginHint">"mobile" | "aadhaar" | "abha-number".</param>
        /// <param name="otpSystem">"abdm" (ABHA-linked mobile) | "aadhaar" (UIDAI Aadhaar OTP).</param>
        Task<AbdmOtpTxnResult> RequestLoginOtpAsync(string loginId, string loginHint, string otpSystem, CancellationToken cancellationToken);

        Task<AbdmProfileResult> VerifyLoginOtpAsync(string txnId, string otp, CancellationToken cancellationToken);
    }
}
