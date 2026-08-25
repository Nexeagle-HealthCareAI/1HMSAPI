namespace EasyHMSAPI.Application.Services
{
    public record PatientVisitHistory(string PatientId, string FullName, bool MarketingConsent, List<DateTime> VisitDates);

    public record LapsedPatientEntry(
        string PatientId,
        string FullName,
        bool MarketingConsent,
        int VisitCount,
        DateTime LastVisitDate,
        int DaysSinceLastVisit,
        decimal AverageGapDays
    );

    /// <summary>
    /// Pure, deterministic lapsed-patient detection -- no AI involved here; Groq only drafts a
    /// generic outreach message template from the aggregate count (see
    /// GroqPatientChurnInsightService), never sees per-patient data.
    /// </summary>
    public static class PatientChurnAnalyzer
    {
        private const int MinVisitsToBeConsideredRegular = 2; // a one-time visitor isn't "lapsed" -- they just haven't been back yet
        private const int LapsedFloorDays = 60;
        private const decimal LapsedRelativeMultiplier = 1.5m; // must be overdue relative to their OWN rhythm, not just a flat floor

        public static List<LapsedPatientEntry> FindLapsedPatients(List<PatientVisitHistory> patients, DateTime asOfDate)
        {
            var result = new List<LapsedPatientEntry>();

            foreach (var patient in patients)
            {
                if (patient.VisitDates.Count < MinVisitsToBeConsideredRegular) continue;

                var ordered = patient.VisitDates.OrderBy(d => d).ToList();
                var lastVisit = ordered[^1];
                var daysSinceLastVisit = (asOfDate.Date - lastVisit.Date).Days;
                if (daysSinceLastVisit < LapsedFloorDays) continue;

                var gaps = new List<decimal>();
                for (var i = 1; i < ordered.Count; i++) gaps.Add((decimal)(ordered[i] - ordered[i - 1]).TotalDays);
                var averageGapDays = gaps.Count > 0 ? gaps.Average() : 0m;

                // Still within this patient's own normal rhythm (e.g. someone who visits every 58
                // days isn't "lapsed" the moment they cross the flat 60-day floor).
                if (averageGapDays > 0m && daysSinceLastVisit < averageGapDays * LapsedRelativeMultiplier) continue;

                result.Add(new LapsedPatientEntry(
                    patient.PatientId,
                    patient.FullName,
                    patient.MarketingConsent,
                    ordered.Count,
                    lastVisit,
                    daysSinceLastVisit,
                    Math.Round(averageGapDays, 1)
                ));
            }

            return result.OrderByDescending(r => r.DaysSinceLastVisit).ToList();
        }
    }
}
