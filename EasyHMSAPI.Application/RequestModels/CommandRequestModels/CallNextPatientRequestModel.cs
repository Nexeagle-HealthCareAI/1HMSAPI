using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Doctor calls the next WAITING patient for today, ordered purely by QueueSequence (the hybrid
    // slot-time-then-arrival-order rule is already baked into QueueSequence at check-in time --
    // see QueueCheckInHelper).
    [ExcludeFromCodeCoverage]
    public class CallNextPatientRequestModel : IRequest<CallQueueResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid DoctorId { get; set; }
    }
}
