using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientVolumeForecastResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public PatientVolumeForecastData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientVolumeForecastData
    {
        // All figures below are computed deterministically from historical appointment data (see
        // PatientVolumeTrendCalculator) -- Groq narrates them, it never invents the numbers.
        public decimal PredictedNext7DayAppointments { get; set; }
        public decimal PredictedNext7DayUniquePatients { get; set; }
        public decimal Avg7DayAppointments { get; set; }
        public decimal Avg30DayAppointments { get; set; }
        public decimal Avg7DayUniquePatients { get; set; }
        public decimal Avg30DayUniquePatients { get; set; }
        public decimal MonthOverMonthAppointmentChangePercent { get; set; }
        public decimal MonthOverMonthUniquePatientChangePercent { get; set; }
        public string Outlook { get; set; } = string.Empty;
        public List<SpecialtyTrendItem> SpecialtyTrends { get; set; } = new();
        public List<DoctorLoadForecastItem> DoctorLoadForecast { get; set; } = new();
        public List<AnomalyFlagItem> Anomalies { get; set; } = new();
        public List<string> Insights { get; set; } = new();
        public List<PatientVolumeTrendPoint> HistoricalTrend { get; set; } = new();
        public List<PatientVolumeTrendPoint> ProjectedTrend { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SpecialtyTrendItem
    {
        public string SpecialtyName { get; set; } = string.Empty;
        public decimal ChangePercent { get; set; }
        public bool IsSurging { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DoctorLoadForecastItem
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public decimal PredictedNext7DayAppointments { get; set; }
        public decimal MonthOverMonthChangePercent { get; set; }
        public bool IsOverloaded { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AnomalyFlagItem
    {
        public string MetricName { get; set; } = string.Empty;
        public decimal RecentValue { get; set; }
        public decimal BaselineMean { get; set; }
        public decimal BaselineStdDev { get; set; }
        public decimal ZScore { get; set; }
        public string Direction { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    public class PatientVolumeTrendPoint
    {
        public DateTime Date { get; set; }
        public int TotalAppointments { get; set; }
        public int UniquePatients { get; set; }
    }
}
