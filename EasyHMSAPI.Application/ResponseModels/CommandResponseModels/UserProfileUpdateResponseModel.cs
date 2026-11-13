using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UserProfileUpdateResponseModel
    {
        public bool Success { get; set; }
        /// <summary>True when the caller is not allowed to act on the target user (controller maps to 403).</summary>
        public bool Forbidden { get; set; }
        public string? Message { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string>? UpdatedFields { get; set; }
        public List<string>? Errors { get; set; }
    }
}
