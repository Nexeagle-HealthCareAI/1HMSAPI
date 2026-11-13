using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordHealthWikiDeliveryResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        // The message id belongs to a Health Wiki message (the webhook also passes on messages of other features).
        public bool Matched { get; set; }
        // The record changed (a later or repeated callback may change nothing).
        public bool Applied { get; set; }
    }
}
