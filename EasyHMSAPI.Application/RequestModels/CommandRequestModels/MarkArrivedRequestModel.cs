using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Reception staff override: issues a queue token for a patient who can't self-check-in (no
    // smartphone location, WhatsApp issue, etc.) -- same token issuance as IssueQueueTokenRequestModel,
    // no geofence check.
    [ExcludeFromCodeCoverage]
    public class MarkArrivedRequestModel : IRequest<IssueQueueTokenResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid DoctorId { get; set; }
    }
}
