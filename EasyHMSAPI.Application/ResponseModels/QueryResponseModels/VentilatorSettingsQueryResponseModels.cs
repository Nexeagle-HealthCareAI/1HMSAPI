using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetVentilatorSettingsHistoryResponseModel
    {
        public List<VentilatorSettingsDataModel> Settings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VentilatorSettingsDataModel
    {
        public Guid VentilatorSettingsId { get; set; }
        public string Mode { get; set; } = null!;
        public decimal? FiO2Percent { get; set; }
        public decimal? PeepCmH2o { get; set; }
        public decimal? TidalVolumeMl { get; set; }
        public int? RespiratoryRateSet { get; set; }
        public decimal? PeakInspiratoryPressure { get; set; }
        public decimal? PlateauPressure { get; set; }
        public string ScoredBy { get; set; } = null!;
        public DateTime ScoredAt { get; set; }
        public string? Notes { get; set; }
    }
}
