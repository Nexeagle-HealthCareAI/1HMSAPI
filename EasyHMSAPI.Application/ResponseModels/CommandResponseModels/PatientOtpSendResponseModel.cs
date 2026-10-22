using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PatientOtpSendResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        // Only ever set on a Development server with PatientAuth:ShowOtpOnScreen on, so a tester need not wait for WhatsApp. Absent everywhere else.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DevCode { get; set; }
    }
}
