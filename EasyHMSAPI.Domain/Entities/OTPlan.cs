using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class OTPlan
    {
        public Guid OtPlanId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string PlanName { get; set; } = null!;
        public string ProcedureName { get; set; } = null!;
        public string? DefaultRoomCategory { get; set; }
        public string? SuggestedIcuLevel { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
