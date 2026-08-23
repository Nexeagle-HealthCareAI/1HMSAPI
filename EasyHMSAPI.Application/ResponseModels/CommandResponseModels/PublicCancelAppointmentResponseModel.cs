using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    // Deliberately minimal — same "anyone with just the AppointmentId can reach this" trust
    // model as GetPublicAppointmentResponseModel, so nothing here should leak PII beyond what
    // the caller already supplied in the request.
    [ExcludeFromCodeCoverage]
    public class PublicCancelAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string? FinalStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
