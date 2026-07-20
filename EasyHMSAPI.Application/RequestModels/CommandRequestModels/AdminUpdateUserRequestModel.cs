using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Update an existing team member by an admin.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AdminUpdateUserRequestModel : IRequest<AdminUpdateUserResponseModel>
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string MobileNumber { get; set; } = null!;
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();          // Doctor / AdminDoctor / Receptionist / Nurse / Admin / Accountant
        public Guid HospitalId { get; set; }
        public string? EmployeeId { get; set; }

        // Doctor details — required only when Roles contains Doctor/AdminDoctor.
        public string? LicenseNumber { get; set; }
        public List<string>? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Department { get; set; }
        public List<string>? Specializations { get; set; }
        // Optional link into the NMC qualification-ladder catalog (dbo.MedicalSpecialities) —
        // additive, sits alongside Qualification/Department/Specializations above.
        public Guid? PrimaryMedicalSpecialityId { get; set; }
        public decimal? ConsultFee { get; set; }   // optional OPD consultation fee

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
