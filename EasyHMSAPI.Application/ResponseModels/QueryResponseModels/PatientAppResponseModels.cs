using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PatientAppProfileResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;

        public string? PreferredLanguage { get; set; }
        public string? HomeCity { get; set; }
        public string? HomeState { get; set; }
        public decimal? HomeLatitude { get; set; }
        public decimal? HomeLongitude { get; set; }
        public Guid? DefaultMemberId { get; set; }
        public string? PreferredTimeWindow { get; set; }
        public Guid? LastDoctorId { get; set; }
        public DateTime? LastBookedAt { get; set; }
        public bool RemindersOptIn { get; set; }
        public bool OnboardingCompleted { get; set; }

        // True once the shared account profile (PatientAccountProfiles) has a first name, age and sex — the three things a booking needs.
        // The app shows its short "About you" step until this is true.
        public bool HasShortProfile { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientAppDeviceResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;
        public Guid? DeviceId { get; set; }
    }
}
