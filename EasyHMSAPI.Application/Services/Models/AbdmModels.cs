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
        // The authenticated session for this ABHA holder — pass back into
        // RequestUpdateMobileOtpAsync/UpdateEmailAsync etc. to prove the holder just re-verified
        // (ABDM requires a fresh OTP-backed session for any profile change, not just a stored ABHA
        // number). Its X-Token is cached server-side, keyed by this TxnId — never sent to the client.
        public string TxnId { get; set; } = string.Empty;
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        // Only populated by GetProfileAsync (§9 Get Profile) — base64 JPEG, as returned by ABDM.
        public string? ProfilePhoto { get; set; }
    }

    /// <summary>Result of a plain (non-OTP) profile field update, e.g. email.</summary>
    public class AbdmUpdateResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>Raw binary payload from a profile-scoped GET that returns an image/PDF rather than
    /// JSON (§10 Generate QR Code, §11 Generate ABHA Card) — passed through as-is, content type and
    /// all, rather than re-encoded.</summary>
    public class AbdmBinaryResult
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
    }

    /// <summary>One ABHA candidate returned by §7.6 Find ABHA's search step — a mobile/Aadhaar can be
    /// linked to more than one ABHA number, so the holder picks one by Index before an OTP is sent.</summary>
    public class AbdmFindAbhaCandidate
    {
        public int Index { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Gender { get; set; }
    }

    public class AbdmFindAbhaSearchResult
    {
        public string TxnId { get; set; } = string.Empty;
        public List<AbdmFindAbhaCandidate> Candidates { get; set; } = new();
    }
}
