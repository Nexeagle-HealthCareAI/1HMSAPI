using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ResetCredentialsResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TempPassword { get; set; }
        public string? FullName { get; set; }
        public string? MobileNumber { get; set; }
        public string? Email { get; set; }
        public string? RoleName { get; set; }
    }
}
