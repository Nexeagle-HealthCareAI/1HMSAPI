using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetActiveAdmissionsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ActiveAdmissionItem> Items { get; set; } = new();
    }

    /// <summary>One currently-open admission (any non-terminal StatusCode), with its patient and
    /// current bed (if assigned) folded in — the real-data counterpart to the bed board, keyed on
    /// admissions instead of beds so an admission with no bed yet is still visible.</summary>
    [ExcludeFromCodeCoverage]
    public class ActiveAdmissionItem
    {
        public Guid AdmissionId { get; set; }
        public string AdmissionNo { get; set; } = null!;
        public string? AdmissionType { get; set; }
        public string StatusCode { get; set; } = null!;
        public string PayerType { get; set; } = null!;
        public DateTime AdmittedAt { get; set; }
        public DateTime? ExpectedDischargeAt { get; set; }
        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }
        public decimal? DepositExpected { get; set; }

        public Guid? PrimaryDoctorId { get; set; }
        public string? PrimaryDoctorName { get; set; }

        public string? ReferralSource { get; set; }
        public string? ReferralName { get; set; }
        public string? ReferringFacilityName { get; set; }
        public string? ReferringFacilityType { get; set; }
        public string? ReferringFacilityContact { get; set; }

        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public short? PatientAge { get; set; }
        public string? PatientSex { get; set; }

        // Current bed, if any — null means this admission hasn't had a bed assigned yet.
        public string? BedCode { get; set; }
        public string? WardName { get; set; }

        public Guid? EncounterId { get; set; }

        // Full coverage detail, when captured (TPA/SCHEME, or a CASH admission with coverage added
        // later). EntitledRoomCategory also drives the bed-entitlement warning at assign/transfer
        // time (client-side check, see roomEntitlement.ts).
        public string? PayerName { get; set; }
        public string? PolicyOrBeneficiaryNo { get; set; }
        public string? PreAuthNo { get; set; }
        public string? PackageCode { get; set; }
        public decimal? SanctionedAmount { get; set; }
        public string? EntitledRoomCategory { get; set; }
    }
}
