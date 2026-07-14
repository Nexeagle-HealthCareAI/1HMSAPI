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
        public string? AdmissionToken { get; set; }
        // Elective only: patient hasn't physically arrived yet — creates the admission as PRE_ADMIT
        // instead of ADMITTED (bed pre-block/pre-auth can still happen now; confirm arrival later).
        public bool IsPreRegistration { get; set; }

        // Referral (for MIS — commission/incentive tracking, distinct from ReferringFacility* below)
        public string? ReferralSource { get; set; }  // SELF / DOCTOR / HOSPITAL
        public string? ReferralName { get; set; }
        public Guid? ReferredByReferrerId { get; set; }

        // Referral / transfer-in (structured): which outside facility sent this patient. Distinct
        // from ReferralSource/ReferralName above, which track referral commission, not provenance.
        public string? ReferringFacilityName { get; set; }
        public string? ReferringFacilityType { get; set; }   // PHC / NURSING_HOME / HOSPITAL / OTHER
        public string? ReferringFacilityContact { get; set; }

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
        // The patient's entitled ward/room category under this scheme/policy (e.g. GENERAL) — drives
        // the bed-entitlement warning at assignment time and the TPA-split proportionate deduction.
        public string? EntitledRoomCategory { get; set; }

        // Optional OT Plan picked in the admit wizard — pre-fills EntitledRoomCategory (when not
        // explicitly supplied) and snapshots ProcedureName/SuggestedIcuLevel onto the admission.
        public Guid? OtPlanId { get; set; }
        // Free-text plan name used only when the desired plan isn't in the OT Plan master list and
        // OtPlanId is left empty — stored directly into OtPlanProcedureNameSnapshot, same as a real
        // OT Plan's ProcedureName would be. Ignored if OtPlanId is also supplied.
        public string? CustomOtPlanText { get; set; }
        // Optional Package Type picked in the admit wizard, from the hospital's Package Type master
        // (same master used by Advise Admission / OT Plan editor) — snapshotted (name) onto the
        // admission at admit time, same freeze pattern as OT Plan.
        public Guid? PackageTypeId { get; set; }
        // Optional — set when admitting from a Referred Admissions board row. On success, that
        // referral is atomically marked CONVERTED and linked to this admission.
        public Guid? ReferralId { get; set; }
    }
}
