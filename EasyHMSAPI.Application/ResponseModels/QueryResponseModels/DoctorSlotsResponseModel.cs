namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class DoctorSlotsResponseModel
    {
        public Guid DoctorId { get; set; }
        public DateTime RequestedDate { get; set; }
        public bool IsTimeOff { get; set; }
        public string? TimeOffReason { get; set; }
        public List<ShiftInfoModel>? ShiftInfo { get; set; }
    }

    public class ShiftInfoModel
    {
        public DateOnly ShiftDate { get; set; }
        public string? DataSource { get; set; }
        public List<ShiftDayDetailsModel>? ShiftDayDetails { get; set; }
    }

    public class ShiftDayDetailsModel
    {
        public Guid? OverrideId { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int SlotDurationInMinutes { get; set; }
        public string? RecurringDays { get; set; }
    }
}
