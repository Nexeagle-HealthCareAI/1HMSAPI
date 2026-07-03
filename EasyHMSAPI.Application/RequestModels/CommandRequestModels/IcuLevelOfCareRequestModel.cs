using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Insert-only — re-assess by inserting a new row, latest wins (mirrors PreOpAssessment).
    [ExcludeFromCodeCoverage]
    public class RecordLevelOfCareRequestModel : IRequest<RecordLevelOfCareResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public string Level { get; set; } = null!;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
    }
}
