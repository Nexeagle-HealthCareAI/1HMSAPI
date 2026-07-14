using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Reassigns the admission's "Referred by": releases the current ACTIVE
    // AdmissionReferrerAssignment row (stamps UnassignedAt/By) and inserts a new ACTIVE one,
    // atomically -- same transactional shape as ChangeAdmittingDoctorRequestModel. Also updates
    // Admission.ReferralSource/ReferralName/ReferredByReferrerId (the live fields every other
    // consumer reads). ReferrerId/ReferrerName/ReferrerType are omitted for SELF.
    [ExcludeFromCodeCoverage]
    public class ChangeAdmissionReferrerRequestModel : IRequest<ChangeAdmissionReferrerResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public string ReferralSource { get; set; } = null!;   // SELF / DOCTOR / OTHER
        public Guid? ReferrerId { get; set; }
        public string? ReferrerName { get; set; }
        public string? ReferrerType { get; set; }
        public string? Notes { get; set; }
    }
}
