namespace EasyHMSAPI.Application.Services
{
    /// <summary>A single doctor's own 90-day series, forecast via PatientVolumeTrendCalculator.Compute().</summary>
    public record DoctorLoadForecastEntry(
        Guid DoctorId,
        string DoctorName,
        decimal PredictedNext30DayAppointments,
        decimal MonthOverMonthChangePercent,
        bool IsOverloaded
    );

    /// <summary>One day's operational counts, used by KpiAnomalyDetector -- separate from
    /// DailyPatientCount because anomaly detection needs no-show/cancelled tallies that the patient
    /// volume forecast doesn't.</summary>
    public record DailyOperationalStats(DateTime Date, int TotalAppointments, int NoShowCount, int CancelledCount);

    /// <summary>A metric whose most recent 7-day value fell more than the z-score threshold away
    /// from its own historical baseline.</summary>
    public record AnomalyFlag(
        string MetricName,
        decimal RecentValue,
        decimal BaselineMean,
        decimal BaselineStdDev,
        decimal ZScore,
        string Direction
    );
}
