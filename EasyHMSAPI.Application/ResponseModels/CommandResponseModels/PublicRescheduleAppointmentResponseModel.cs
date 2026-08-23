using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PublicRescheduleAppointmentResponseModel
    {
        public bool Success { get; set; }
        public Guid ApptId { get; set; }
        public string? FinalStatus { get; set; }
        public TokenInfo? Token { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
