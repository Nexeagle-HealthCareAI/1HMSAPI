using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorSpecializationsResponseModel
    {
        public Guid DepartmentId { get; set; }
        public Guid? HospitalId { get; set; }
        public bool IncludeGlobal { get; set; }
        public List<SpecializationItem> Items { get; set; } = new List<SpecializationItem>();
    }

    [ExcludeFromCodeCoverage]
    public class SpecializationItem
    {
        public Guid SpecializationId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
