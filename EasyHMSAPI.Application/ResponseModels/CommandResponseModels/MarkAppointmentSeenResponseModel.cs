using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class MarkAppointmentSeenResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>When the hospital first saw it (UTC) — this call's time, or the earlier one if already seen.</summary>
        public DateTime? HospitalSeenAt { get; set; }
    }
}
