using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordVitalReadingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? VitalReadingId { get; set; }
    }
}
