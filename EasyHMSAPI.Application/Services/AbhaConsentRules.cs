using System.Security.Cryptography;
using System.Text;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// The consent wording shown before an Aadhaar-based ABHA enrolment, and the rules for using a recorded consent.
    /// The wording lives HERE (the server is the source of truth, the web fetches it) so replacing it with NHA's current published text
    /// is one edit plus a version bump.
    ///
    /// NOTE: this is the wording the product already shipped with (CRT_ABHA_102). It has NOT been checked against NHA's current published
    /// copy; that is a compliance step to complete before production go-live.
    /// </summary>
    public static class AbhaConsentRules
    {
        public const string Code = "abha-enrollment";
        public const string Version = "1.4";
        public const string PurposeEnrolment = "ABHA_ENROLMENT";
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        /// <summary>The first OTP plus two resends (CRT_ABHA_106).</summary>
        public const int MaxOtpRequestsPerConsent = 3;

        public const string Text =
            "I hereby declare that I am voluntarily sharing my Aadhaar number and demographic information issued by UIDAI, with the National Health Authority (NHA), for the sole purpose of creating an Ayushman Bharat Health Account (ABHA) number and ABHA Address. I understand that my Aadhaar number / Virtual ID and demographic information will be used only for this purpose and will not be used for any other purpose. This consent is given in accordance with the provisions of the Aadhaar Act, 2016 and the regulations made thereunder. I understand that my personally identifiable information (name, address, age, date of birth, gender, photograph, mobile number) may be shared with entities in the National Digital Health Ecosystem that I choose to interact with, only after my informed consent.";

        public static string TextSha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));

        /// <summary>
        /// Looks up a consent and checks it may be used for one more OTP request by this caller at this hospital. Returns the consent, or a
        /// message explaining why not. Does not change anything.
        /// </summary>
        public static async Task<(AbhaConsent? Consent, string? Error)> ValidateForOtpAsync(
            AppDbContext context, Guid? consentId, Guid hospitalId, Guid callerUserId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            const string missing = "Record the patient's consent before requesting an OTP.";
            if (consentId == null || consentId == Guid.Empty) return (null, missing);

            var consent = await context.AbhaConsent.FirstOrDefaultAsync(c => c.AbhaConsentId == consentId.Value, cancellationToken);
            // Same message for "not found" and "somebody else's" so consent ids cannot be probed.
            if (consent == null || consent.HospitalId != hospitalId || consent.GrantedByUserId != callerUserId) return (null, missing);

            if (!string.Equals(consent.ConsentCode, Code, StringComparison.Ordinal) || !string.Equals(consent.ConsentVersion, Version, StringComparison.Ordinal))
                return (null, "The consent wording has been updated. Record the consent again.");
            if (nowUtc - consent.CreatedAt > Lifetime)
                return (null, "The recorded consent has expired. Record the consent again.");
            if (consent.OtpRequestCount >= MaxOtpRequestsPerConsent)
                return (null, "The OTP limit for this consent has been reached. Record a new consent to start again.");

            return (consent, null);
        }
    }
}
