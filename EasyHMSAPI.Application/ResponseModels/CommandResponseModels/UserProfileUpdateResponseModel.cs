using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UserProfileUpdateResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string>? UpdatedFields { get; set; }
        public List<string>? Errors { get; set; }
    }
}
