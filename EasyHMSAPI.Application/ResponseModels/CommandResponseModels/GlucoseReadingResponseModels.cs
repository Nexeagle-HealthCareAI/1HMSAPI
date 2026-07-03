using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordGlucoseReadingResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? GlucoseReadingId { get; set; }
        public decimal? ValueMgDl { get; set; }
        public bool IsHypo { get; set; }
        public bool IsHyper { get; set; }
    }
}
