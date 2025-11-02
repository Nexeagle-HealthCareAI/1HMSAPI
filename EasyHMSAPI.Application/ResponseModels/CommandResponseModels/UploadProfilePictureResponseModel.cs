using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadProfilePictureResponseModel
    {
        public bool Success { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
