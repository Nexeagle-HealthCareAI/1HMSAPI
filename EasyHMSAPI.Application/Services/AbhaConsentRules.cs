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

        /// <summary>NHA's terms and conditions for ABHA enrolment, word for word (hiecm.concept.m1-abha-consent,
        /// CRT_ABHA_102). The consent block of enrol/byAadhaar, code abha-enrollment version 1.4, records agreement to it.</summary>
        public const string Text =
            "I, hereby declare that I am voluntarily sharing my Aadhaar number and demographic information issued by UIDAI, with National Health Authority (NHA) for the sole purpose of creation of ABHA number. I understand that my ABHA number can be used and shared for purposes as may be notified by ABDM from time to time including provision of healthcare services. Further, I am aware that my personal identifiable information (Name, Address, Age, Date of Birth, Gender and Photograph) may be made available to the entities working in the National Digital Health Ecosystem (NDHE) which inter alia includes stakeholders and entities such as healthcare professionals (e.g. doctors), facilities (e.g. hospitals, laboratories) and data fiduciaries (e.g. health programmes), which are registered with or linked to the Ayushman Bharat Digital Mission (ABDM), and various processes there under. I authorize NHA to use my Aadhaar number for performing Aadhaar based authentication with UIDAI as per the provisions of the Aadhaar (Targeted Delivery of Financial and other Subsidies, Benefits and Services) Act, 2016 for the aforesaid purpose. I understand that UIDAI will share my e-KYC details, or response of \u201cYes\u201d with NHA upon successful authentication. I have been duly informed about the option of using other IDs apart from Aadhaar; however, I consciously choose to use Aadhaar number for the purpose of availing benefits across the NDHE. I am aware that my personal identifiable information excluding Aadhaar number / VID number can be used and shared for purposes as mentioned above. I reserve the right to revoke the given consent at any point of time as per provisions of Aadhaar Act and Regulations.";

        /// <summary>The facility's consent language: five declarations, each with its own checkbox (CRT_ABHA_102).
        /// A private entity removes the word "government", as NHA asks. In an Aadhaar flow the second stays unchecked.</summary>
        public static readonly string[] Declarations =
        {
            "I am voluntarily sharing my Aadhaar Number / Virtual ID issued by the Unique Identification Authority of India (\u201cUIDAI\u201d), and my demographic information for the purpose of creating an Ayushman Bharat Health Account number (\u201cABHA number\u201d) and Ayushman Bharat Health Account address (\u201cABHA Address\u201d). I authorize NHA to use my Aadhaar number / Virtual ID for performing Aadhaar based authentication with UIDAI as per the provisions of the Aadhaar (Targeted Delivery of Financial and other Subsidies, Benefits and Services) Act, 2016 for the aforesaid purpose. I understand that UIDAI will share my e-KYC details, or response of \u201cYes\u201d with NHA upon successful authentication.",
            "I intend to create Ayushman Bharat Health Account Number (\u201cABHA number\u201d) and Ayushman Bharat Health Account address (\u201cABHA Address\u201d) using document other than Aadhaar.",
            "I consent to usage of my ABHA address and ABHA number for linking of my legacy (past) health records and those which will be generated during this encounter.",
            "I authorize the sharing of all my health records with healthcare provider(s) for the purpose of providing healthcare services to me during this encounter.",
            "I consent to the anonymization and subsequent use of my health records for public health purposes.",
        };

        /// <summary>Which declarations an Aadhaar enrolment needs ticked: all but the second (creation by another document).</summary>
        public static readonly bool[] AadhaarFlowDeclarations = { true, false, true, true, true };

        public static string WorkerConfirmation(string? workerName) =>
            $"I, {Name(workerName, "the healthcare worker")}, confirm that I have duly informed and explained the beneficiary of the contents of consent for aforementioned purposes.";

        public static string BeneficiaryConfirmation(string? beneficiaryName) =>
            $"I, {Name(beneficiaryName, "the beneficiary")}, have been explained about the consent as stated above and hereby provide my consent for the aforementioned purposes.";

        private static string Name(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();

        /// <summary>What is kept as evidence: the terms, each declaration as ticked, and both confirmations with the names in them.</summary>
        public static string Record(bool[] declarations, string? workerName, string? beneficiaryName, DateTime atUtc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Terms and Conditions").AppendLine(Text).AppendLine().AppendLine("I hereby declare that:");
            for (var i = 0; i < Declarations.Length; i++)
                sb.Append(declarations.Length > i && declarations[i] ? "[x] " : "[ ] ").AppendLine(Declarations[i]);
            sb.Append("[x] ").AppendLine(WorkerConfirmation(workerName));
            sb.Append("[x] ").AppendLine(BeneficiaryConfirmation(beneficiaryName));
            sb.Append("Recorded at ").Append(atUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")).AppendLine(" (UTC)");
            return sb.ToString();
        }

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
