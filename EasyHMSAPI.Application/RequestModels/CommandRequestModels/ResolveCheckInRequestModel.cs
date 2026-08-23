using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Walk-in check-in: resolves "this phone number's appointment today at this hospital" without
    // the caller needing to already know an AppointmentId (unlike IssueQueueTokenRequestModel).
    // Geofence-gated before any PatientRegistrations lookup runs -- see ResolveCheckInHandler.
    [ExcludeFromCodeCoverage]
    public class ResolveCheckInRequestModel : IRequest<ResolveCheckInResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}
