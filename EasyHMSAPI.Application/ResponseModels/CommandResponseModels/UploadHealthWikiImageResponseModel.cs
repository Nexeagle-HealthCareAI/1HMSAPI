using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadHealthWikiImageResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // The https address to put in the article (cover or inline).
        public string? Url { get; set; }

        public static UploadHealthWikiImageResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
