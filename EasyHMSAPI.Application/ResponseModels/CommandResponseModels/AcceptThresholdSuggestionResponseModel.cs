using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AcceptThresholdSuggestionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Set only when RequestingStoreId was supplied and stock was actually below the new max --
        // lets the UI show/link the real Indent that was raised, instead of a vague confirmation.
        public Guid? IndentId { get; set; }
        public string? IndentNumber { get; set; }
    }
}
