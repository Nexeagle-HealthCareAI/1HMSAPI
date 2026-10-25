using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class VisitGuideModel
    {
        public string? Landmark { get; set; }
        public string? EntranceNote { get; set; }
        public string? OpdLocation { get; set; }
        public string? RegistrationDesk { get; set; }
        public string? ParkingNote { get; set; }
        public string? PaymentNote { get; set; }
        public string? ExtraNote { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HospitalVisitGuideResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public VisitGuideModel Guide { get; set; } = new();
        /// <summary>The hospital has a map pin, so patients can check in from their phone on arrival.</summary>
        public bool HasMapPin { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicVisitToken
    {
        public int? TokenNo { get; set; }
        public string? Status { get; set; }
        public int? CurrentServingTokenNo { get; set; }
        public int? PositionInQueue { get; set; }
        public int? EstimatedWaitMinutes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicVisit
    {
        public Guid AppointmentId { get; set; }
        /// <summary>The short booking ID shown to the patient and quoted at the counter ("ABCDEF12").</summary>
        public string Reference { get; set; } = null!;
        public string StatusCode { get; set; } = "";
        public string Status { get; set; } = "";
        /// <summary>The hospital has confirmed the time (no longer a request).</summary>
        public bool Confirmed { get; set; }
        public bool Cancelled { get; set; }
        public bool Completed { get; set; }
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public Guid HospitalId { get; set; }
        public string HospitalName { get; set; } = "";
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Contact { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string MapUrl { get; set; } = "";
        public string Date { get; set; } = "";
        /// <summary>"HH:mm" (hospital time), or null when the booking has no time.</summary>
        public string? Time { get; set; }
        public bool IsToday { get; set; }
        public decimal? Fee { get; set; }
        public VisitGuideModel Guide { get; set; } = new();
        /// <summary>Check in from the phone (POST /public/tokens with the patient's location): today, open, the hospital has a pin, not
        /// checked in yet.</summary>
        public bool CanCheckIn { get; set; }
        public PublicVisitToken? Token { get; set; }
        public string? FollowUpDate { get; set; }
        public string? Language { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicVisitResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public PublicVisit? Visit { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RunVisitMessagesResponseModel
    {
        public int Sent { get; set; }
        public int NotSent { get; set; }
        public int Skipped { get; set; }
    }
}
