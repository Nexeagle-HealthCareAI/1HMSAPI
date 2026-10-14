using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicPatientProfileResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public int? Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? Sex { get; set; }
        public string? Email { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
        // ACCOUNT (saved by the patient on Doctor Dekho) | BOOKING (from the latest hospital registration) | NONE
        public string Source { get; set; } = "NONE";
        public int StatusCode { get; set; } = 200;
    }
}
