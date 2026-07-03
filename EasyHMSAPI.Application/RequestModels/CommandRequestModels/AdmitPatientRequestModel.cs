using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AdmitPatientRequestModel : IRequest<AdmitPatientResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        // Patient: a UHID (PatientId) => an existing patient (demographics are refreshed);
        // omitted => a brand-new patient is registered and a UHID is auto-generated.
        public string? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? Age { get; set; }
        public string? AgeUnit { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? BloodGroup { get; set; }
        public string? Religion { get; set; }
        public string? Nationality { get; set; }

        // Address (granular)
        public string? FlatHouse { get; set; }
        public string? Street { get; set; }
        public string? AddressLine { get; set; }
        public string? Block { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? Country { get; set; }

        // Contact
        public string? AlternateMobile { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Government IDs (optional)
        public string? AadhaarNumber { get; set; }
        public string? PanNumber { get; set; }
        public string? AbhaId { get; set; }

        // Admission
        public string? AdmissionType { get; set; }   // EMERGENCY / ELECTIVE / DAYCARE / LAMA
        public Guid? PrimaryDoctorId { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public DateTime? ExpectedDischargeAt { get; set; }
        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }

        // Referral (for MIS)
        public string? ReferralSource { get; set; }  // SELF / DOCTOR / HOSPITAL
        public string? ReferralName { get; set; }
        public Guid? ReferredByReferrerId { get; set; }

        // ── Payer branch (Phase 1: CASH is fully wired; TPA/SCHEME are capture-only) ──
        public string? PayerType { get; set; }        // CASH / TPA / SCHEME (default CASH)
        public decimal? DepositExpected { get; set; } // planned deposit (cash flow)
        // Open an IPD billing encounter so charges/day-wise bills accrue to the stay (default true).
        public bool EnableIpdBilling { get; set; } = true;
        // Offline resync idempotency: a re-sent admit with the same id returns the existing admission.
        public Guid? ClientRequestId { get; set; }

        // Optional bed to assign at admit time.
        public Guid? BedId { get; set; }

        // Coverage detail (stored when TPA / SCHEME, or when any of these is supplied).
        public string? PayerName { get; set; }
        public string? PolicyOrBeneficiaryNo { get; set; }
        public string? PreAuthNo { get; set; }
        public string? PackageCode { get; set; }
        public decimal? SanctionedAmount { get; set; }
    }
}
