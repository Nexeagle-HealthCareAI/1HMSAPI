using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PublicAppointmentSummary? Appointment { get; set; }
    }
}
