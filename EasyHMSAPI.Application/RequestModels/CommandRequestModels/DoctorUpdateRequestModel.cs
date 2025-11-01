using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DoctorUpdateRequestModel : MediatR.IRequest<DoctorUpdateResponseModel>
    {
        public Guid UserId { get; set; }
        public Guid HospitalDepartmentMappingId { get; set; }
        public string? LicenseNumber { get; set; }
        public List<string>? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Bio { get; set; }
        public string? PrimaryDepartment { get; set; }
        public string? Department { get; set; }
        public List<string>? Specializations { get; set; }
    }
}
