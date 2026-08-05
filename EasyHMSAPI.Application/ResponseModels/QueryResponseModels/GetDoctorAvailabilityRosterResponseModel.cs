using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorAvailabilityRosterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<DoctorAvailabilityRosterItem> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DoctorAvailabilityRosterItem
    {
        public Guid DoctorId { get; set; }
        public string? FullName { get; set; }
        public string? DepartmentName { get; set; }
        public bool IsAvailable { get; set; }
        // Only set when IsAvailable is false and the doctor has a TimeOff entry covering the
        // requested date (matches GetPublicDoctorAvailabilityResponseModel's Reason field).
        public string? Reason { get; set; }
        // The DoctorTimeOffs row driving Reason above, if any - lets the roster cancel it
        // directly via DELETE calendar/doctor/timeoff/{timeOffId} without a second lookup.
        public Guid? TimeOffId { get; set; }
        public DateTime? TimeOffFromDate { get; set; }
        public DateTime? TimeOffToDate { get; set; }
        // Manual "online now" toggle (Doctor.IsOnlineNow) — separate from IsAvailable, which is
        // schedule-derived.
        public bool IsOnlineNow { get; set; }
    }
}
