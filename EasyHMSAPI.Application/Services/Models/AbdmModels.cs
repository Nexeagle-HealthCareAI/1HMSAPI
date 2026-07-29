namespace EasyHMSAPI.Application.Services.Models
{
    /// <summary>Result of an OTP-generate call — just enough for the caller to prompt for the OTP.</summary>
    public class AbdmOtpTxnResult
    {
        public string TxnId { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    /// <summary>Demographic snapshot returned by ABDM after Aadhaar-OTP enrolment / mobile verification.
    /// Never carries the ABDM X-Token — that stays server-side, cached against TxnId.</summary>
    public class AbdmEnrollResult
    {
        public string TxnId { get; set; } = string.Empty;
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public bool MobileVerified { get; set; }
        public bool IsNew { get; set; }
    }

    public class AbdmAddressSuggestionsResult
    {
        public string TxnId { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>Profile snapshot for an existing ABHA holder logging in / linking (mobile or
    /// Aadhaar OTP), or for a freshly created ABHA account.</summary>
    public class AbdmProfileResult
    {
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
    }
}
