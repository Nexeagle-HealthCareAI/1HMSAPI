using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffListResponseModel
    {
        public Guid DoctorId { get; set; }
        public List<DoctorTimeOffItem> TimeOffs { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffItem
    {
        public Guid TimeOffId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }
        public bool IsUpcoming { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
