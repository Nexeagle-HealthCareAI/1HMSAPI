using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class RequestSurgeryRequestModel : IRequest<RequestSurgeryResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public string ProcedureName { get; set; } = null!;
        public string SurgeryType { get; set; } = null!;
        public string Urgency { get; set; } = null!;
        public Guid? SurgeonDoctorId { get; set; }
        public string? SurgeonName { get; set; }
        public Guid? AnaesthetistDoctorId { get; set; }
        public string? AnaesthetistName { get; set; }
    }

    // Advances/cancels a SurgeryCase. Valid ToStatus values follow the fixed sequence
    // REQUESTED->SCHEDULED->PRE_OP->IN_THEATRE->POST_OP->COMPLETED (SCHEDULED is normally reached
    // via CreateOTBookingRequestModel instead), or CANCELLED from any non-terminal state. Also
    // syncs the case's active OTBooking: IN_THEATRE marks it IN_PROGRESS; leaving IN_THEATRE (to
    // POST_OP or CANCELLED) marks it COMPLETED/CANCELLED, freeing the theatre.
    [ExcludeFromCodeCoverage]
    public class UpdateSurgeryCaseStatusRequestModel : IRequest<UpdateSurgeryCaseStatusResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public string ToStatus { get; set; } = null!;
        public string? Reason { get; set; }
    }
}
