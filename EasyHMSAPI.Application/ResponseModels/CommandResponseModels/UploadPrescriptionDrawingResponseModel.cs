using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UploadPrescriptionDrawingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DrawingId { get; set; }
        public string? FileUrl { get; set; }
        public int SequenceNo { get; set; }
    }
}
