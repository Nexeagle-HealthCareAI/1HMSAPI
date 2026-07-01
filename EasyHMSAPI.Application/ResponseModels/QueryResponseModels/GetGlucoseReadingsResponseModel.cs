using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetGlucoseReadingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<GlucoseReadingItem> Readings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GlucoseReadingItem
    {
        public Guid GlucoseReadingId { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; } = null!;
        public decimal ValueMgDl { get; set; }
        public string? Method { get; set; }
        public string? MealTag { get; set; }
        public bool InsulinGiven { get; set; }
        public decimal? InsulinUnits { get; set; }
        public string? InsulinType { get; set; }
        public string? InsulinRoute { get; set; }
        public bool IsHypo { get; set; }
        public bool IsHyper { get; set; }
        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public string? Notes { get; set; }
    }
}
