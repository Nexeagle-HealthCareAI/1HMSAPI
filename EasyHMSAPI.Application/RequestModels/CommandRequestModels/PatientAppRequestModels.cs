using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // The Doctor Dekho app saves its quick-booking preferences. Every field is optional: a field left out (null) keeps its saved value,
    // so the app can send only what changed (for example just LastDoctorId after a booking). Mobile comes from the verified token.
    [ExcludeFromCodeCoverage]
    public class SavePatientAppProfileRequestModel : IRequest<PatientAppProfileResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        // en | hi | bn | hg
        public string? PreferredLanguage { get; set; }
        public string? HomeCity { get; set; }
        public string? HomeState { get; set; }
        public decimal? HomeLatitude { get; set; }
        public decimal? HomeLongitude { get; set; }
        // Who the patient usually books for. Send ClearDefaultMember = true to go back to "myself".
        public Guid? DefaultMemberId { get; set; }
        public bool? ClearDefaultMember { get; set; }
        // MORNING | AFTERNOON | EVENING | NIGHT
        public string? PreferredTimeWindow { get; set; }
        public Guid? LastDoctorId { get; set; }
        public bool? RemindersOptIn { get; set; }
        public bool? OnboardingCompleted { get; set; }
    }

    // The app registers (or refreshes) this install after sign-in and whenever its push token changes.
    [ExcludeFromCodeCoverage]
    public class RegisterPatientAppDeviceRequestModel : IRequest<PatientAppDeviceResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        public string InstallationId { get; set; } = string.Empty;
        // android | ios
        public string Platform { get; set; } = string.Empty;
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public string? DeviceModel { get; set; }
        public string? PushToken { get; set; }
    }

    // On sign-out the app switches this install off so no reminder reaches a phone the patient has left.
    [ExcludeFromCodeCoverage]
    public class DeactivatePatientAppDeviceRequestModel : IRequest<PatientAppDeviceResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        public string InstallationId { get; set; } = string.Empty;
    }
}
