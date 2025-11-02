using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorOverrideCreateRequestModel : MediatR.IRequest<DoctorOverrideCreateResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime OverrideDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ShiftDetails>? ShiftDetails{ get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ShiftDetails
    {
        public string? ShiftName { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int SlotDurationInMinutes { get; set; }
        public List<string>? RecurringDays { get; set; }
    }
}
