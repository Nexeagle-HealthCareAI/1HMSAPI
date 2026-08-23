using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public record BillingInsightNarrative(string Outlook, List<string> Insights);

    /// <summary>
    /// Narrates already-computed billing trend numbers (see BillingTrendCalculator) into a short
    /// outlook sentence and a handful of natural-language insights. Never asked to invent the
    /// numbers themselves -- only to explain/highlight what the numbers already show.
    /// </summary>
    public interface IBillingInsightService
    {
        Task<BillingInsightNarrative> GenerateInsightsAsync(TrendSummary trendSummary);
    }
}
