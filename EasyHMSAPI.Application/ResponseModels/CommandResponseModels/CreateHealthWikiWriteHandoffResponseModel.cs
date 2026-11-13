using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateHealthWikiWriteHandoffResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // The Doctor Dekho address to open. Works once, for a minute.
        public string? Url { get; set; }

        public static CreateHealthWikiWriteHandoffResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
