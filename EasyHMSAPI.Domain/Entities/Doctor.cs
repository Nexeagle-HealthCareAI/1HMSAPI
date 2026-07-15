using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Doctor
    {
        [Key]
        public Guid DoctorID { get; set; }
        public Guid UserID { get; set; }
        public string LicenseNumber { get; set; } = null!;
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Bio { get; set; }
        public int? ProfileCompletionPercent { get; set; }
        public string? ObjectURL { get; set; }
        // JSON array of spoken languages, e.g. ["English","Hindi"]. Kept as JSON (not CSV) so
        // the DB-level ISJSON() check constraint gives free integrity validation.
        public string? LanguagesJson { get; set; }
        // Deliberately separate from User.Email/User.MobileNumber, which are login/OTP
        // credentials — these are optional, admin-set fields for public display only.
        public string? PublicContactEmail { get; set; }
        public string? PublicContactPhone { get; set; }
        public Guid? PrimaryDepartmentID { get; set; }
        public Guid? HospitalId { get; set; } // Added hospitalId
        // Opt-in: doctor only appears in the platform-wide public directory when BOTH
        // their hospital (Hospital.IsPubliclyListed) AND this flag are true.
        public bool IsPubliclyListed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public Department? PrimaryDepartment { get; set; }
        public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();
        public ICollection<DoctorShiftOverride> DoctorShiftOverrides { get; set; } = new List<DoctorShiftOverride>();
        public ICollection<DoctorTimeOff> DoctorTimeOffs { get; set; } = new List<DoctorTimeOff>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<DoctorQueue> DoctorQueues { get; set; } = new List<DoctorQueue>();
    }
}
