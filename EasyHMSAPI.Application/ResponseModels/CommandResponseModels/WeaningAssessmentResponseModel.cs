using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordWeaningAssessmentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? WeaningAssessmentId { get; set; }
    }
}
