using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordAbhaConsentResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>Pass this with the Aadhaar OTP request.</summary>
        public Guid? ConsentId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
