using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Hospital
    {
        [Key]
        public Guid HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string RegistrationNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string Contact { get; set; } = null!;
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string Location { get; set; } = null!;
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string Pincode { get; set; } = null!;
        public string? TimeZone { get; set; }
        public bool IsActive { get; set; } = true;
        // Opt-in: hospital only appears in the platform-wide public doctor directory
        // (Nexeagle's "find a doctor" page) once this is explicitly turned on.
        public bool IsPubliclyListed { get; set; } = false;
        public Guid CreatedByUserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public string? GSTIN { get; set; }
        public string? PAN { get; set; }
        public string? NABH_NABL { get; set; }

        // Chain grouping: null = standalone hospital; set = member of an owner's hospital chain.
        public Guid? ChainId { get; set; }
        public HospitalChain? Chain { get; set; }

        public User CreatedByUser { get; set; } = null!;
        public ICollection<HospitalUser> HospitalUsers { get; set; } = new List<HospitalUser>();
        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<Department> Departments { get; set; } = new List<Department>();
        public ICollection<Specialization> Specializations { get; set; } = new List<Specialization>();
        public ICollection<HospitalDepartmentMapping> HospitalDepartmentMappings { get; set; } = new List<HospitalDepartmentMapping>();
        public HospitalProfileStatus HospitalProfileStatus { get; set; } = null!;
        public PrescriptionHeaderFooter PrescriptionHeaderFooter { get; set; } = null!;
        public HospitalSetting HospitalSetting { get; set; } = null!;
        public ICollection<UserInvitation> UserInvitations { get; set; } = new List<UserInvitation>();
        //public ICollection<PatientRegistration> PatientRegistrations { get; set; } = new List<PatientRegistration>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<DoctorQueue> DoctorQueues { get; set; } = new List<DoctorQueue>();
    }
}
