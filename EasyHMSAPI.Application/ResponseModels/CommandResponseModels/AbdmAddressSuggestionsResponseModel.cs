using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmAddressSuggestionsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TxnId { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }
}
