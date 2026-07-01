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
}
