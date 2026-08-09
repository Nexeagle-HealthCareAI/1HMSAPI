using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetWeaningAssessmentHistoryResponseModel
    {
        public List<WeaningAssessmentDataModel> Assessments { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class WeaningAssessmentDataModel
    {
        public Guid WeaningAssessmentId { get; set; }
        public bool SatPerformed { get; set; }
        public bool SatPassed { get; set; }
        public bool SbtPerformed { get; set; }
        public bool SbtPassed { get; set; }
        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }
        public string? Notes { get; set; }
    }
}
