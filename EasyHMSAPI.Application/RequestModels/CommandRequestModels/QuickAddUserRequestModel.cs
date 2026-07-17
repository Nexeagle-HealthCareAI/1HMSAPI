using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Direct "quick add" of a team member by an admin — no invitation link / OTP. Creates the user
    /// with a password set by the admin, assigns the role, adds them to the current hospital, and
    /// (for Doctor/AdminDoctor) creates the doctor profile.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class QuickAddUserRequestModel : IRequest<QuickAddUserResponseModel>
    {
        public string FullName { get; set; } = null!;
        public string MobileNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string Password { get; set; } = null!;
        public List<string> Roles { get; set; } = new();          // Doctor / AdminDoctor / Receptionist / Nurse / Admin / Accountant
        public Guid HospitalId { get; set; }
        public string? EmployeeId { get; set; }

        // Doctor details — required only when Roles contains Doctor/AdminDoctor.
        public string? LicenseNumber { get; set; }
        public List<string>? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public string? Department { get; set; }
        public List<string>? Specializations { get; set; }
        // Optional link into the NMC qualification-ladder catalog (dbo.MedicalSpecialities) —
        // additive, sits alongside Qualification/Department/Specializations above.
        public Guid? PrimaryMedicalSpecialityId { get; set; }
        public decimal? ConsultFee { get; set; }   // optional OPD consultation fee

        [JsonIgnore]
        public Guid CallerUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
