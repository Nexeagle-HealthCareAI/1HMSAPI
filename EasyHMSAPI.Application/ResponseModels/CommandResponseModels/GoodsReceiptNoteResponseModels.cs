using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateGoodsReceiptNoteResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? GrnId { get; set; }
        public string? GrnNumber { get; set; }
        public string? MatchStatus { get; set; }
    }
}
