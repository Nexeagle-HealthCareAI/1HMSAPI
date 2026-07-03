using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRoundNotesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<RoundNoteItem> Notes { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RoundNoteItem
    {
        public Guid RoundNoteId { get; set; }
        public Guid? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public DateTime NotedAt { get; set; }
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? Diagnosis { get; set; }
        public bool IsAddendum { get; set; }
        public Guid? ParentNoteId { get; set; }
        public string? AddendumReason { get; set; }
    }
}
