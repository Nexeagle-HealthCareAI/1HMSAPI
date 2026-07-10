using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // "Is the doctor generally working this day" only — no granular open-slot computation, since
    // a public pre-appointment doesn't claim a real time slot (that only happens when front desk
    // confirms it).
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorAvailabilityResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool IsAvailable { get; set; }
        public string? Reason { get; set; }
        public List<PublicShiftInfo> Shifts { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicShiftInfo
    {
        public string? Name { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }
}
