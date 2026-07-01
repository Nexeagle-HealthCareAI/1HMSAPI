using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateShiftHandoverNoteResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ShiftHandoverNoteId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AcknowledgeShiftHandoverResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
