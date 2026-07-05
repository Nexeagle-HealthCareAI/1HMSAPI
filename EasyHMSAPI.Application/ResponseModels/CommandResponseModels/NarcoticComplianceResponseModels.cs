using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DispenseNarcoticResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public decimal? NewCurrentStock { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordColdChainReadingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool BreachFlag { get; set; }
    }
}
