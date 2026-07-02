using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateOperationTheatreResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? TheatreId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateOTBookingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OTBookingId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RescheduleOTBookingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CancelOTBookingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
