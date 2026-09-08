using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Registers a patient with no appointment or admission attached -- every other
    /// registration path (RegisterAppointmentHandler, AdmitPatientHandler) bundles patient
    /// creation with booking a slot or a bed, which doesn't fit a walk-in lab visit that has
    /// neither. Reuses AppointmentBookingHelpers.FindOrCreatePatientAsync directly, the same
    /// mobile+name matching every other path already relies on.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegisterWalkInPatientRequestModel : IRequest<RegisterWalkInPatientResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Patient? Patient { get; set; }
        // Who registered this walk-in, for the same audit trail RegisterAppointmentRequestModel's
        // UserId already feeds -- client-supplied (the frontend already has it from the auth
        // store), same convention as that sibling request model.
        public Guid? UserId { get; set; }
    }
}
