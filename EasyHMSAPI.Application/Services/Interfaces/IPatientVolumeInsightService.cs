using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public record PatientVolumeInsightNarrative(string Outlook, List<string> Insights);

    /// <summary>Everything the narrative service is allowed to talk about -- the core trend
    /// summary plus the doctor-load and anomaly signals computed alongside it. Kept separate from
    /// PatientVolumeTrendSummary itself so that record (and its unit tests) never need to change
    /// just because the narrative gains a new input.</summary>
    public record PatientVolumeInsightContext(
        PatientVolumeTrendSummary Trend,
        List<DoctorLoadForecastEntry> DoctorLoadForecast,
        List<AnomalyFlag> Anomalies,
        decimal NoShowRate
    );

    /// <summary>
    /// Narrates already-computed patient volume trend numbers (see PatientVolumeTrendCalculator)
    /// into a short outlook sentence and a handful of natural-language insights. Never asked to
    /// invent the numbers themselves -- only to explain/highlight what the numbers already show.
    /// </summary>
    public interface IPatientVolumeInsightService
    {
        Task<PatientVolumeInsightNarrative> GenerateInsightsAsync(PatientVolumeInsightContext context);
    }
}
