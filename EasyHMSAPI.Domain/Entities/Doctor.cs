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
        // Optional link into the NMC qualification-ladder catalog (dbo.MedicalSpecialities) —
        // e.g. "DM Cardiology". Additive: sits alongside the free-text Qualification field and
        // the separate Department/Specialization system above, replacing neither. Its only
        // consumer today is GetPublicDoctorsHandler, which prefers this speciality's
        // PatientFacingCategory over fuzzy-matching Department.Name for Doctor Dekho search.
        public Guid? PrimaryMedicalSpecialityId { get; set; }
        public Guid? HospitalId { get; set; } // Added hospitalId
        // Opt-in: doctor only appears in the platform-wide public directory when BOTH
        // their hospital (Hospital.IsPubliclyListed) AND this flag are true.
        public bool IsPubliclyListed { get; set; } = false;

        // ── CMS-controlled Doctor Dekho marketing/moderation fields ──────────────────
        // Scheduled consultation-fee discount. "Active" is never stored as its own bool —
        // always computed at read time from these three (see DoctorMarketingHelpers) so it
        // can't drift out of sync with the date window.
        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStartAt { get; set; }
        public DateTime? DiscountEndAt { get; set; }
        // Top-of-listing placement on the public Doctor Dekho directory.
        public bool IsFeatured { get; set; } = false;
        // Platform-level override, deliberately SEPARATE from IsPubliclyListed above (which is
        // the hospital's own opt-in). A hospital re-enabling IsPubliclyListed must never silently
        // undo a CMS delisting, and vice versa — final visibility is
        // Hospital.IsPubliclyListed && Doctor.IsPubliclyListed && !Doctor.IsDelistedByAdmin.
        public bool IsDelistedByAdmin { get; set; } = false;
        // Set by a CMS admin only after manually confirming this doctor's LicenseNumber/
        // MedicalCouncil/RegistrationYear against the NMC's Indian Medical Register (no automated
        // verification API exists in India). Drives the public "Verified profile" badge on
        // Doctor Dekho. Verified*At/ByUserId are set only on the false->true transition and
        // cleared when unmarked — see CMSAPI's DoctorRepository.UpdateDoctorMarketingAsync.
        public bool IsRegistrationVerified { get; set; } = false;
        public DateTime? RegistrationVerifiedAt { get; set; }
        public Guid? RegistrationVerifiedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public Department? PrimaryDepartment { get; set; }
        public MedicalSpeciality? PrimaryMedicalSpeciality { get; set; }
        public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();
        public ICollection<DoctorShiftOverride> DoctorShiftOverrides { get; set; } = new List<DoctorShiftOverride>();
        public ICollection<DoctorTimeOff> DoctorTimeOffs { get; set; } = new List<DoctorTimeOff>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<DoctorQueue> DoctorQueues { get; set; } = new List<DoctorQueue>();
    }
}
