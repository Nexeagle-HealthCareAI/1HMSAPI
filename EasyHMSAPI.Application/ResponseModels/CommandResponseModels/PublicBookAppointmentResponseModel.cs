using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PublicBookAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AppointmentId { get; set; }
        public string? PatientId { get; set; }
    }
}
