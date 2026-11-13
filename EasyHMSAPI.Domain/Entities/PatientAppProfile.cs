using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // What the Doctor Dekho mobile app keeps on top of the patient's account profile: quick-booking preferences and reminder consent.
    // The short profile itself (name, age, sex, blood group, email, guardian) stays in PatientAccountProfile so the website and the app
    // share one profile. Keyed by the OTP-verified mobile.
    [ExcludeFromCodeCoverage]
    [Table("PatientAppProfiles")]
    public class PatientAppProfile
    {
        [Key]
        public string Mobile { get; set; } = null!;
        // en | hi | bn | hg
        public string? PreferredLanguage { get; set; }
        public string? HomeCity { get; set; }
        public string? HomeState { get; set; }
        public decimal? HomeLatitude { get; set; }
        public decimal? HomeLongitude { get; set; }
        // Who the patient usually books for: null = themselves, else one of their PatientFamilyMembers.
        public Guid? DefaultMemberId { get; set; }
        // MORNING | AFTERNOON | EVENING | NIGHT
        public string? PreferredTimeWindow { get; set; }
        public Guid? LastDoctorId { get; set; }
        public DateTime? LastBookedAt { get; set; }
        public bool RemindersOptIn { get; set; }
        public DateTime? RemindersOptInAt { get; set; }
        public DateTime? OnboardingCompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // One install of the Doctor Dekho app on one phone, for one signed-in mobile. Holds the Expo push token so reminders can reach it.
    [ExcludeFromCodeCoverage]
    [Table("PatientAppDevices")]
    public class PatientAppDevice
    {
        [Key]
        public Guid DeviceId { get; set; }
        public string Mobile { get; set; } = null!;
        // A random id the app creates once per install.
        public string InstallationId { get; set; } = null!;
        // android | ios
        public string Platform { get; set; } = null!;
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public string? DeviceModel { get; set; }
        public string? PushToken { get; set; }
        public bool IsActive { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}
