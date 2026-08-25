namespace EasyHMSAPI.Application.Services.Interfaces
{
    public record PatientChurnSummary(int LapsedCount, int ConsentedLapsedCount, List<string> TopSpecialtiesTheyUsedToVisit);

    public record PatientChurnNarrative(string Outlook, string SuggestedOutreachMessage);

    /// <summary>
    /// Drafts a generic re-engagement outreach message from aggregate-only lapsed-patient counts
    /// (see PatientChurnAnalyzer). Deliberately never receives a patient name or any per-patient
    /// data -- the message it drafts is a reusable template, not addressed to anyone specific.
    /// </summary>
    public interface IPatientChurnInsightService
    {
        Task<PatientChurnNarrative> GenerateInsightsAsync(PatientChurnSummary summary);
    }
}
