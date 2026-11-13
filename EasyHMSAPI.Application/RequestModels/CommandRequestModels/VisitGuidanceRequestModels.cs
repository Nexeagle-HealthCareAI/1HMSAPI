using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Vita vision, Phase 4 (after booking): the hospital's patient visit guide, the patient's visit page, and the reminder run.

    [ExcludeFromCodeCoverage]
    public class GetHospitalVisitGuideRequestModel : IRequest<HospitalVisitGuideResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore] public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveHospitalVisitGuideRequestModel : IRequest<HospitalVisitGuideResponseModel>
    {
        [JsonIgnore] public Guid HospitalId { get; set; }
        [JsonIgnore] public Guid CallerUserId { get; set; }
        public string? Landmark { get; set; }
        public string? EntranceNote { get; set; }
        public string? OpdLocation { get; set; }
        public string? RegistrationDesk { get; set; }
        public string? ParkingNote { get; set; }
        public string? PaymentNote { get; set; }
        public string? ExtraNote { get; set; }
    }

    /// <summary>GET /public/appointments/{id}/visit. Like the guest booking lookup, knowing the appointment id is the only gate, so
    /// the answer never carries the patient's name, mobile or reason.</summary>
    [ExcludeFromCodeCoverage]
    public class GetPublicVisitRequestModel : IRequest<PublicVisitResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }

    /// <summary>PUT /public/appointments/{id}/journey: how the patient is coming (drive / auto_cab / transit / walk), when they will
    /// leave ("08:15" on the appointment day, or "yyyy-MM-ddTHH:mm"), and whether reception may see their ETA. Fields left out keep their
    /// value. The appointment id is the only gate, as for the visit page.</summary>
    [ExcludeFromCodeCoverage]
    public class SavePublicJourneyRequestModel : IRequest<PublicJourneyResponseModel>
    {
        [JsonIgnore] public Guid AppointmentId { get; set; }
        public string? TravelMode { get; set; }
        public string? LeaveAt { get; set; }
        public bool? ShareWithHospital { get; set; }
        [JsonIgnore] public DateTime NowUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>POST /public/appointments/{id}/journey/progress: on_the_way (with the ETA in minutes and the distance left), late (by
    /// how many minutes), arrived or cancelled. Never a location.</summary>
    [ExcludeFromCodeCoverage]
    public class UpdatePublicJourneyRequestModel : IRequest<PublicJourneyResponseModel>
    {
        [JsonIgnore] public Guid AppointmentId { get; set; }
        public string? Status { get; set; }
        public int? EtaMinutes { get; set; }
        public decimal? DistanceKm { get; set; }
        public int? LateByMinutes { get; set; }
        [JsonIgnore] public DateTime NowUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>One run of the visit messages job (reminders and the after-visit message).</summary>
    [ExcludeFromCodeCoverage]
    public class RunVisitMessagesRequestModel : IRequest<RunVisitMessagesResponseModel>
    {
        public DateTime NowUtc { get; set; } = DateTime.UtcNow;
    }
}
