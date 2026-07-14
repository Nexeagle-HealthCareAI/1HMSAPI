namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Stateless helper computing "device-days" -- the overlap, in days, between a
    /// device's in-place span and a reporting date range. Feeds the NHSN-style "infections
    /// per 1000 device-days" rate in GetInfectionRateSummaryHandler.</summary>
    public static class DeviceDaysCalculator
    {
        public static decimal ComputeOverlapDays(DateTime insertedAt, DateTime removedAtOrNow, DateTime rangeStart, DateTime rangeEnd)
        {
            var start = insertedAt > rangeStart ? insertedAt : rangeStart;
            var end = removedAtOrNow < rangeEnd ? removedAtOrNow : rangeEnd;
            if (end <= start)
                return 0m;

            return (decimal)(end - start).TotalDays;
        }
    }
}
