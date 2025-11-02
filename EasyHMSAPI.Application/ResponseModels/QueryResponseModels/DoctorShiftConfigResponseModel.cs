using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorShiftConfigResponseModel
    {
        public Guid DoctorId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public List<ShiftInfo>? ShiftInfo { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ShiftInfo
    {
        public DateOnly ShiftDate { get; set; }
        public string? DataSource { get; set; }
        public List<ShiftDayDetails>? ShiftDayDetails { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ShiftDayDetails
    {
        public Guid? OverrideId { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int SlotDurationInMinutes { get; set; }
        public string? RecurringDays { get; set; }
    }
}
