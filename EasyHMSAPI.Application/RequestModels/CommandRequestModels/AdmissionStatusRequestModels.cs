using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Basic discharge: closes an active admission straight to DISCHARGED, stamps DischargedAt/By/Notes,
    // and releases the current bed if any. The full auto-summary/TPA-split/IRDAI-clock discharge bundle
    // is a later phase — this is just the status close-out.
    [ExcludeFromCodeCoverage]
    public class DischargeAdmissionRequestModel : IRequest<DischargeAdmissionResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public DateTime? DischargedAt { get; set; }
        public string? DischargeNotes { get; set; }
    }

    // Generic transition for every other exit/interim status (DISCHARGE_INITIATED, DISCHARGE_BILLED,
    // LAMA, DAMA, TRANSFERRED_OUT, EXPIRED, CANCELLED). DISCHARGED goes through DischargeAdmission
    // instead, so its notes/timestamp are always captured. Terminal transitions auto-release the bed.
    // EXPIRED must never be blocked here by billing state (IRDAI immediate body-release) — this
    // handler doesn't check billing at all, by design.
    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionStatusRequestModel : IRequest<UpdateAdmissionStatusResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public string ToStatus { get; set; } = null!;
        public string? Reason { get; set; }
    }

    // Confirms a PRE_ADMIT (elective pre-registration) admission has physically arrived: flips it
    // to ADMITTED, stamps AdmittedAt to now, and optionally assigns a bed in the same transaction
    // (nested-mediator call into AssignBedRequestModel — reuses the existing race-safe assignment
    // logic rather than duplicating it). Dedicated handler, same reasoning as DischargeAdmission
    // being separate from the generic transition: this has side effects beyond a status flip.
    [ExcludeFromCodeCoverage]
    public class ConfirmPatientArrivalRequestModel : IRequest<ConfirmPatientArrivalResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? BedId { get; set; }
    }

    // Edits fields captured at admission time, after the fact — only while the admission is still
    // Active (a closed/historical admission's record stays fixed). Same skip-null-or-blank
    // convention as AdmitPatientHandler.ApplyDemographics: a field omitted/blank leaves the
    // existing value untouched (this is for adding/correcting details, not clearing them back out).
    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionDetailsRequestModel : IRequest<UpdateAdmissionDetailsResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }

        public Guid? PrimaryDoctorId { get; set; }
        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }
        public DateTime? ExpectedDischargeAt { get; set; }
        public string? PayerType { get; set; }
        public decimal? DepositExpected { get; set; }
        public string? ReferralSource { get; set; }
        public string? ReferralName { get; set; }
        public string? ReferringFacilityName { get; set; }
        public string? ReferringFacilityType { get; set; }
        public string? ReferringFacilityContact { get; set; }
    }

    // Upserts the admission's AdmissionCoverage row (create if none exists yet — e.g. a CASH
    // admission converted to TPA/SCHEME after the fact — else update in place).
    [ExcludeFromCodeCoverage]
    public class UpsertAdmissionCoverageRequestModel : IRequest<UpsertAdmissionCoverageResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }

        public string? PayerName { get; set; }
        public string? PolicyOrBeneficiaryNo { get; set; }
        public string? PreAuthNo { get; set; }
        public string? PackageCode { get; set; }
        public decimal? SanctionedAmount { get; set; }
        public string? EntitledRoomCategory { get; set; }
    }
}
