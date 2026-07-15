using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorCreateRequestModel : MediatR.IRequest<DoctorCreateResponseModel>
    {
        public Guid UserId { get; set; }
        public string LicenseNumber { get; set; } = null!;
        public List<string>? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Bio { get; set; }
        public string? PrimaryDepartment { get; set; }
        public string? Department { get; set; }
        public List<string>? Specializations { get; set; }
        public Guid? HospitalId { get; set; }
        public List<string>? Languages { get; set; }
        public string? PublicContactEmail { get; set; }
        public string? PublicContactPhone { get; set; }
    }
}
