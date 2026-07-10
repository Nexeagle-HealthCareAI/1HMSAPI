using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ConfirmPreAppointmentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AppointmentId { get; set; }
        public string? Status { get; set; }
        public int? TokenNumber { get; set; }
    }
}
