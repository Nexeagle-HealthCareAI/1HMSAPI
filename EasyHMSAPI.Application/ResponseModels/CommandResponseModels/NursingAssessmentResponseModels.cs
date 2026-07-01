using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordNursingAssessmentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? NursingAssessmentId { get; set; }
        public int MorseTotal { get; set; }
        public string? MorseRisk { get; set; }
        public int BradenTotal { get; set; }
        public string? BradenRisk { get; set; }
        public int MustTotal { get; set; }
        public string? MustRisk { get; set; }
    }
}
