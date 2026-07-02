using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetOperationTheatresResponseModel
    {
        public List<OperationTheatreDataModel> Theatres { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OperationTheatreDataModel
    {
        public Guid TheatreId { get; set; }
        public string TheatreCode { get; set; } = null!;
        public string TheatreName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetOTScheduleResponseModel
    {
        public List<OTBookingDataModel> Bookings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OTBookingDataModel
    {
        public Guid OTBookingId { get; set; }
        public Guid SurgeryCaseId { get; set; }
        public Guid TheatreId { get; set; }
        public string? TheatreCode { get; set; }
        public string? TheatreName { get; set; }
        public string? ProcedureName { get; set; }
        public string? PatientName { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string StatusCode { get; set; } = null!;
    }
}
