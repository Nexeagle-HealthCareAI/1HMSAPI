using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Public (Nexeagle) booking request. Deliberately thin compared to RegisterAppointmentRequestModel:
    // no AppointmentId (always creates), no StartAt/SlotTimeInMinutes/AllocateToken (a public booking
    // never claims a real time slot — PreferredDate/PreferredTime are non-binding, the receptionist
    // picks the real StartAt at confirm time). HospitalId is not a field here at all — the handler
    // resolves it from DoctorId (+ Hospital.IsPubliclyListed), never from a client-supplied value.
    [ExcludeFromCodeCoverage]
    public class PublicBookAppointmentRequestModel : IRequest<PublicBookAppointmentResponseModel>
    {
        public Patient? Patient { get; set; }
        public Guid DoctorId { get; set; }
        public DateTime PreferredDate { get; set; }
        public TimeSpan? PreferredTime { get; set; }
        public string? Reason { get; set; }

        // Booking-attribution metadata — where the visitor came from. ReferrerUrl/UtmCampaign are
        // only knowable client-side (document.referrer, landing-page query string), so the Nexeagle
        // frontend supplies them. IpAddress is resolved server-side from the request connection
        // (PublicController) — never trusted from the client body, same reasoning as HospitalId.
        public string? ReferrerUrl { get; set; }
        public string? UtmCampaign { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }

        // The OTP-verified patient-session mobile, resolved server-side (PublicController) from the
        // Authorization header via IPatientTokenValidator — never trusted from the client body. NULL
        // when there's no valid session, meaning this is a guest booking.
        [JsonIgnore]
        public string? VerifiedMobile { get; set; }
    }
}
