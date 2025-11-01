namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class DoctorSpecializationsResponseModel
    {
        public Guid DepartmentId { get; set; }
        public Guid? HospitalId { get; set; }
        public bool IncludeGlobal { get; set; }
        public List<SpecializationItem> Items { get; set; } = new List<SpecializationItem>();
    }

    public class SpecializationItem
    {
        public Guid SpecializationId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
