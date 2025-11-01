using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
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
        public Guid? PrimaryDepartmentID { get; set; }
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
