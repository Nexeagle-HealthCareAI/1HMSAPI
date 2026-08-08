using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // One shift's SAT/SBT assessment -- data capture only, standard ABCDEF-bundle data points.
    [ExcludeFromCodeCoverage]
    public class RecordWeaningAssessmentRequestModel : IRequest<RecordWeaningAssessmentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

        public bool SatPerformed { get; set; }
        public bool SatPassed { get; set; }
        public bool SbtPerformed { get; set; }
        public bool SbtPassed { get; set; }

        public string? Notes { get; set; }
    }
}
