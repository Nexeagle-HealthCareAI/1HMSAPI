using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorBookedSlotsResponseModel
    {
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
        public List<TimeSpan> BookedSlots { get; set; } = new List<TimeSpan>();
    }
}
