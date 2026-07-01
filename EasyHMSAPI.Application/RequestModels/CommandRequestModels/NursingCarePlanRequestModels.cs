using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateNursingCarePlanItemRequestModel : IRequest<CreateNursingCarePlanItemResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string NursingDiagnosis { get; set; } = null!;
        public string? Goal { get; set; }
        public string? PlannedInterventions { get; set; }
    }

    // Resolves (or discontinues) an active care-plan item — mirrors ClinicalOrderCommandHandlers'
    // DiscontinueClinicalOrderLine lifecycle shape.
    [ExcludeFromCodeCoverage]
    public class ResolveNursingCarePlanItemRequestModel : IRequest<ResolveNursingCarePlanItemResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid CarePlanItemId { get; set; }
        public string StatusCode { get; set; } = null!;   // RESOLVED / DISCONTINUED
        public string? ResolutionNotes { get; set; }
    }
}
