using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    /// <summary>One place as a patient sees it: the numbers to call, how far, where.</summary>
    [ExcludeFromCodeCoverage]
    public class PublicEmergencyPlaceModel
    {
        public Guid SettingId { get; set; }
        public Guid? HospitalId { get; set; }
        public string Name { get; set; } = null!;
        public string Kind { get; set; } = "HOSPITAL";
        public string Phone { get; set; } = null!;
        public string? AlternatePhone { get; set; }
        public double DistanceKm { get; set; }
        public string? Address { get; set; }
        public string? Landmark { get; set; }
        public string? City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool Is24x7 { get; set; }
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyPlacesResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public List<PublicEmergencyPlaceModel> Places { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RaiseEmergencyAlertResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? EventId { get; set; }
        public List<PublicEmergencyPlaceModel> Places { get; set; } = new();
        /// <summary>How many places got the alert (WhatsApp or in-app).</summary>
        public int Alerted { get; set; }
        /// <summary>This session already alerted in the last 30 minutes: the same event, nothing sent again.</summary>
        public bool Repeated { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyActionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int Count { get; set; }
    }

    /// <summary>A place with everything the hospital form and the CMS show (never shown to patients as is).</summary>
    [ExcludeFromCodeCoverage]
    public class EmergencyPlaceModel
    {
        public Guid? SettingId { get; set; }
        public Guid? HospitalId { get; set; }
        public string Source { get; set; } = "HOSPITAL";
        public string? Name { get; set; }
        public string Kind { get; set; } = "HOSPITAL";
        public bool IsEnabled { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? AlternatePhone { get; set; }
        public string? AlertWhatsApp { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Landmark { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool Is24x7 { get; set; } = true;
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public string? DutyContactName { get; set; }
        public string? DutyContactPhone { get; set; }
        public string? TermsVersion { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public string VerificationStatus { get; set; } = "PENDING";
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public string? VerificationNote { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>True while patients can see it: switched on and verified.</summary>
        public bool Listed { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HospitalEmergencySettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public EmergencyPlaceModel? Settings { get; set; }
        /// <summary>The terms version a hospital must accept now; when it differs from Settings.TermsVersion the form asks again.</summary>
        public string CurrentTermsVersion { get; set; } = null!;
        public string[] CapabilityOptions { get; set; } = Array.Empty<string>();
        /// <summary>The settings were saved and now wait for a CMS admin to check them.</summary>
        public bool SentForVerification { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyPlaceListResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public List<EmergencyPlaceModel> Items { get; set; } = new();
        public int Pending { get; set; }
        public int Verified { get; set; }
        public int Rejected { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyAlertLogPlace
    {
        public Guid SettingId { get; set; }
        public string Name { get; set; } = null!;
        public decimal? DistanceKm { get; set; }
        public bool WhatsAppSent { get; set; }
        public bool InApp { get; set; }
        public int CallTaps { get; set; }
        public string? Acknowledged { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyAlertLogEvent
    {
        public Guid EventId { get; set; }
        public DateTime At { get; set; }
        public string? Reason { get; set; }
        public string? Channel { get; set; }
        /// <summary>Masked ("98xxxxxx10") when the patient chose to share it, else null.</summary>
        public string? SharedMobile { get; set; }
        public List<EmergencyAlertLogPlace> Places { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class EmergencyAlertLogResponseModel
    {
        public bool Success { get; set; } = true;
        public List<EmergencyAlertLogEvent> Events { get; set; } = new();
    }
}
