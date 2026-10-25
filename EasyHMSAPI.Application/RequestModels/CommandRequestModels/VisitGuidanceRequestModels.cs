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

    /// <summary>One run of the visit messages job (reminders and the after-visit message).</summary>
    [ExcludeFromCodeCoverage]
    public class RunVisitMessagesRequestModel : IRequest<RunVisitMessagesResponseModel>
    {
        public DateTime NowUtc { get; set; } = DateTime.UtcNow;
    }
}
