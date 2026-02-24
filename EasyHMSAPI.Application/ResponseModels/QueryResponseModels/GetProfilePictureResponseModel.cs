using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetProfilePictureResponseModel
    {
        public bool Success { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
