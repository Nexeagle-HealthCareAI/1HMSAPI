using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Doctor skips the currently-CALLED-but-absent patient for today. Re-queues them 3 positions
    // later (AppConstants.QueueSkipRequeueOffset); capped at AppConstants.QueueMaxSkipsPerToken
    // skips per token, past which this is a hard stop requiring manual reception handling.
    [ExcludeFromCodeCoverage]
    public class SkipCurrentPatientRequestModel : IRequest<CallQueueResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid DoctorId { get; set; }
    }
}
