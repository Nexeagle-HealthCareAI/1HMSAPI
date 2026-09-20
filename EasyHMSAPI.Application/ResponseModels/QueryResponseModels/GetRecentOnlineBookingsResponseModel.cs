using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRecentOnlineBookingsResponseModel
    {
        /// <summary>Server clock (UTC) when this was computed -- the client's next `since` cursor,
        /// so polling never depends on the browser's own (often wrong) clock.</summary>
        public DateTime ServerTime { get; set; }

        /// <summary>Public (Doctor Dekho / NexEagle) bookings created after the cursor, newest first.</summary>
        public List<RecentOnlineBookingItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RecentOnlineBookingItem
    {
        public Guid AppointmentId { get; set; }
        public string? PatientName { get; set; }
        public string? DoctorName { get; set; }
        public DateTime ApptDate { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Status { get; set; }
    }
}
