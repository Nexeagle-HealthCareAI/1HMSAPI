using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateRoundNoteResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? RoundNoteId { get; set; }
        public bool IsAddendum { get; set; }
        public Guid? ParentNoteId { get; set; }
    }
}
