using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Patient self-check-in via the OPD QR flow: converts a booked appointment into a queue token,
    // after verifying the reported GPS position is within the hospital's geofence. Idempotent -- a
    // retried scan for an appointment that already has a token just returns the existing one.
    [ExcludeFromCodeCoverage]
    public class IssueQueueTokenRequestModel : IRequest<IssueQueueTokenResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}
