using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Doctor self-service "online now" toggle. CallerUserId is resolved server-side from the
    // JWT's "userId" claim (see UserContextHelper.GetUserId), never trusted from client input —
    // there is no hospitalId query param on this endpoint, so HospitalAccessFilter fail-opens and
    // this JWT-derived identity resolution is the actual security boundary (see
    // EPrescriptionController.ParseVoiceRx for the same pattern).
    [ExcludeFromCodeCoverage]
    public class UpdateOwnOnlineStatusRequestModel : IRequest<UpdateDoctorOnlineStatusResponseModel>
    {
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
        public bool IsOnlineNow { get; set; }
    }
}
