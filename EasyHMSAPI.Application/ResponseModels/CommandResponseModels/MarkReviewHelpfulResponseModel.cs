using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class MarkReviewHelpfulResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int HelpfulCount { get; set; }
    }
}
