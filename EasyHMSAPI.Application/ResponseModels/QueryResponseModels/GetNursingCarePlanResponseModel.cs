using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetNursingCarePlanResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<NursingCarePlanItemModel> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class NursingCarePlanItemModel
    {
        public Guid CarePlanItemId { get; set; }
        public string NursingDiagnosis { get; set; } = null!;
        public string? Goal { get; set; }
        public string? PlannedInterventions { get; set; }
        public string StatusCode { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? ResolutionNotes { get; set; }
    }
}
