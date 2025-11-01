namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class UpdateDepartmentResponseModel
    {
        public Guid DepartmentID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
