using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetVitalReadingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<VitalReadingItem> Readings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VitalReadingItem
    {
        public Guid VitalReadingId { get; set; }
        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }

        public decimal? Temperature { get; set; }
        public string? TemperatureUnit { get; set; }
        public int? Pulse { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? RespiratoryRate { get; set; }
        public decimal? SpO2 { get; set; }

        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI { get; set; }

        public int? GcsEye { get; set; }
        public int? GcsVerbal { get; set; }
        public int? GcsMotor { get; set; }
        public int? GcsTotal { get; set; }

        public int? PainScore { get; set; }
        public string? Notes { get; set; }
    }
}
