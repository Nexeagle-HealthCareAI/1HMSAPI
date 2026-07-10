using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Staff-side "Confirm" action for a PRE_APPOINTMENT row created via the public booking API.
    // This is the genuine slot-commitment moment: the receptionist picks a real StartAt (from
    // actual availability) and it gets validated/locked in here — nothing was reserved when the
    // pre-appointment was first submitted publicly.
    [ExcludeFromCodeCoverage]
    public class ConfirmPreAppointmentRequestModel : IRequest<ConfirmPreAppointmentResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }

        // Bound from the request body (not [JsonIgnore]) so HospitalAccessFilter can verify the
        // signed-in user belongs to this hospital — same convention as CancelAppointmentRequestModel.
        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public DateTime StartAt { get; set; }

        public int? SlotTimeInMinutes { get; set; }
    }
}
