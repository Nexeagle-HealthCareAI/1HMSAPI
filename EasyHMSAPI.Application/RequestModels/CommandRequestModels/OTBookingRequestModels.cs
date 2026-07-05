using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: TheatreId present => update that theatre in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class CreateOperationTheatreRequestModel : IRequest<CreateOperationTheatreResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? TheatreId { get; set; }
        public string TheatreCode { get; set; } = null!;
        public string TheatreName { get; set; } = null!;
        public Guid? DepartmentId { get; set; }
        public decimal Price { get; set; }
        public string? Status { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // Books a theatre/time-slot for a SurgeryCase. Conflict-checked (application-level, not a DB
    // exclusion constraint — SQL Server has none): rejects if the theatre already has an active
    // (SCHEDULED/IN_PROGRESS) booking whose window overlaps the requested one.
    [ExcludeFromCodeCoverage]
    public class CreateOTBookingRequestModel : IRequest<CreateOTBookingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Guid TheatreId { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
    }

    // Updates the existing active booking's theatre/time in place (same row — no new booking
    // history table this phase). Re-runs the same overlap check, excluding the booking's own row.
    [ExcludeFromCodeCoverage]
    public class RescheduleOTBookingRequestModel : IRequest<RescheduleOTBookingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid OTBookingId { get; set; }
        public Guid TheatreId { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CancelOTBookingRequestModel : IRequest<CancelOTBookingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid OTBookingId { get; set; }
        public string? Reason { get; set; }
    }
}
