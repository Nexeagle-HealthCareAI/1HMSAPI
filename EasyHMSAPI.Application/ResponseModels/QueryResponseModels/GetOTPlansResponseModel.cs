using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetOTPlansResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<OTPlanDataModel> Plans { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OTPlanDataModel
    {
        public Guid OtPlanId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? PlanName { get; set; }
        public string? ProcedureName { get; set; }
        public string? DefaultRoomCategory { get; set; }
        public string? SuggestedIcuLevel { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
