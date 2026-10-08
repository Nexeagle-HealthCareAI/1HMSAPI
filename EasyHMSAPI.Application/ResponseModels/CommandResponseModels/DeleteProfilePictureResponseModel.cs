using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteProfilePictureResponseModel
    {
        public bool Success { get; set; }
        /// <summary>True when the caller is not allowed to act on the target user (controller maps to 403).</summary>
        public bool Forbidden { get; set; }
        public string? Message { get; set; }
    }
}
