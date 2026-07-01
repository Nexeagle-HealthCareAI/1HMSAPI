using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetShiftHandoverNotesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ShiftHandoverNoteItem> Notes { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ShiftHandoverNoteItem
    {
        public Guid ShiftHandoverNoteId { get; set; }
        public string ShiftCode { get; set; } = null!;
        public DateTime ShiftDate { get; set; }
        public string OutgoingNurseName { get; set; } = null!;
        public string? IncomingNurseName { get; set; }
        public DateTime? IncomingAckAt { get; set; }
        public bool IsFreeText { get; set; }
        public string? FreeTextNote { get; set; }
        public string? Situation { get; set; }
        public string? Background { get; set; }
        public string? Assessment { get; set; }
        public string? Recommendation { get; set; }
        public DateTime HandoverAt { get; set; }
    }
}
