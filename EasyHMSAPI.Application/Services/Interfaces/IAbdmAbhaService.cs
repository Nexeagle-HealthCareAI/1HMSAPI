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

        // ---- Profile updates — require a freshly-verified session (a TxnId from
        // VerifyLoginOtpAsync/VerifyAadhaarOtpAsync/VerifyMobileOtpAsync whose cached X-Token is
        // still valid, ~20 min) since ABDM requires live holder consent for any profile change. ----

        Task<AbdmOtpTxnResult> RequestUpdateMobileOtpAsync(string sessionTxnId, string newMobile, CancellationToken cancellationToken);

        Task<AbdmUpdateResult> VerifyUpdateMobileOtpAsync(string sessionTxnId, string updateTxnId, string otp, CancellationToken cancellationToken);

        Task<AbdmUpdateResult> UpdateEmailAsync(string sessionTxnId, string newEmail, CancellationToken cancellationToken);

        Task<AbdmProfileResult> GetProfileAsync(string sessionTxnId, CancellationToken cancellationToken);

        // ---- §10/§11: read-only, holder-facing artifacts, both gated by the same live session ----

        Task<AbdmBinaryResult> GetQrCodeAsync(string sessionTxnId, CancellationToken cancellationToken);

        Task<AbdmBinaryResult> GetAbhaCardAsync(string sessionTxnId, CancellationToken cancellationToken);

        // ---- §7.6 Find ABHA — for a holder who doesn't remember their exact ABHA number/address but
        // has the mobile or Aadhaar it's linked to. Search can surface more than one linked ABHA; the
        // caller picks one by index, then completes the same OTP verify as a normal login
        // (VerifyLoginOtpAsync) to actually authenticate as that account. ----

        /// <param name="searchBy">"mobile" | "aadhaar".</param>
        Task<AbdmFindAbhaSearchResult> FindAbhaSearchAsync(string value, string searchBy, CancellationToken cancellationToken);

        Task<AbdmOtpTxnResult> FindAbhaGenerateOtpAsync(string txnId, int index, string searchBy, CancellationToken cancellationToken);

        // ---- §8.4/§8.5: deactivate requires a live, freshly-verified session (same precondition as
        // the profile updates above); reactivate is a cold-start flow since a deactivated account has
        // no live session to begin with. ----

        /// <param name="otpSystem">"aadhaar" (§8.4.1) | "abdm" (§8.4.2, OTP to the ABHA-linked mobile).</param>
        Task<AbdmOtpTxnResult> RequestDeactivateOtpAsync(string sessionTxnId, string abhaNumber, string otpSystem, CancellationToken cancellationToken);

        Task<AbdmUpdateResult> VerifyDeactivateOtpAsync(string sessionTxnId, string deactivateTxnId, string otp, string reason, CancellationToken cancellationToken);

        Task<AbdmOtpTxnResult> RequestReactivateOtpAsync(string abhaNumber, CancellationToken cancellationToken);

        Task<AbdmProfileResult> VerifyReactivateOtpAsync(string txnId, string otp, CancellationToken cancellationToken);
    }
}
