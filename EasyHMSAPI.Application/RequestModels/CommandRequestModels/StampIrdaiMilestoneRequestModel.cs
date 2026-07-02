using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Stamps one of the 2 user-actionable IRDAI clock milestones (claim submitted / insurer
    // approval) on the admission's existing AdmissionCoverage row. The other 2 milestones
    // (discharge decision, physical discharge) are always derived from AdmissionStatusHistory —
    // never stamped here.
    [ExcludeFromCodeCoverage]
    public class StampIrdaiMilestoneRequestModel : IRequest<StampIrdaiMilestoneResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public string MilestoneKey { get; set; } = null!;   // CLAIM_SUBMITTED / INSURER_APPROVAL
        public DateTime? At { get; set; }
    }
}
