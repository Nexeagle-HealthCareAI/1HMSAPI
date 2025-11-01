namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHospitalUsersResponseModel
    {
        public Guid HospitalUserId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public string? EmployeeID { get; set; }
        public string? IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
} 