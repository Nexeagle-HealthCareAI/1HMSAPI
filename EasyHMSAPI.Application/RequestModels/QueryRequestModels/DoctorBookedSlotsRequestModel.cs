using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorBookedSlotsRequestModel : IRequest<DoctorBookedSlotsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; } // Added hospitalId
        public DateTime Date { get; set; }

        // The appointment currently being edited/confirmed, if any — without this, that
        // appointment's OWN (still-uncommitted) StartAt shows up as "booked" against itself,
        // since this query has no other way to tell "the slot I already occupy" apart from a
        // genuine conflict with a different appointment.
        public Guid? ExcludeAppointmentId { get; set; }
    }
}
