using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    // What the patient's own profile page needs to show the contributor tag. Never the mobile number or registration details.
    [ExcludeFromCodeCoverage]
    public class ContributorProfileTagResponseModel
    {
        public bool Success { get; set; } = true;
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public bool IsContributor { get; set; }
        // INDEPENDENT_DOCTOR | HEALTH_WORKER | WRITER
        public string? Role { get; set; }
        // INVITED (not finished) | PENDING | VERIFIED
        public string? Status { get; set; }
        public string? FullName { get; set; }
    }
}
