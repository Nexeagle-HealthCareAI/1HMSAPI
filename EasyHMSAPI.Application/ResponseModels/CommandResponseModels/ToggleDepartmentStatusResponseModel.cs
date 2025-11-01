namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class ToggleDepartmentStatusResponseModel
    {
        public Guid DepartmentID { get; set; }
        public bool IsActive { get; set; }
        public string? Message { get; set; }
    }
}
