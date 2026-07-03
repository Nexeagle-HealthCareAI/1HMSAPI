using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Insert-only — re-assess by recording a new row, latest wins (mirrors NursingAssessment).
    [ExcludeFromCodeCoverage]
    public class RecordPreOpAssessmentRequestModel : IRequest<RecordPreOpAssessmentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public string? AsaGrade { get; set; }
        public bool NpoConfirmed { get; set; }
        public bool AllergiesReviewed { get; set; }
        public bool InvestigationsReviewed { get; set; }
        public bool ConsentConfirmed { get; set; }
        public string? Notes { get; set; }
    }
}
