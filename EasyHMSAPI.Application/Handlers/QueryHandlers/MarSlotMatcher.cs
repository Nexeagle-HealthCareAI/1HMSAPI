namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Nearest-within-tolerance slot matching, extracted out of GetMarGridHandler so it can also
    /// drive the bulk Nursing Station summary -- both surfaces must agree on exactly which
    /// administration satisfies which computed slot, or a nurse's "2 overdue" badge on the station
    /// could silently disagree with what the MAR grid itself shows for the same patient. Pure/
    /// stateless and deliberately decoupled from MedicationAdministration: callers pass lightweight
    /// candidate tuples and get back which slot (if any) each one claimed.
    /// </summary>
    public static class MarSlotMatcher
    {
        public readonly record struct Candidate(Guid Id, DateTime ScheduledFor, DateTime? ActedAt);

        public readonly record struct SlotMatch(DateTime ScheduledForUtc, Guid? MatchedId);

        /// <summary>
        /// For each computed slot (in order), finds the closest not-yet-claimed candidate within
        /// MarScheduleCalculator.MatchTolerance, tie-broken by latest ActedAt. Each candidate can
        /// match at most one slot -- mirrors GetMarGridHandler's original inline claimed-set logic.
        /// </summary>
        public static List<SlotMatch> Match(IEnumerable<DateTime> computedSlots, IEnumerable<Candidate> candidates)
        {
            var claimed = new HashSet<Guid>();
            var candidateList = candidates.ToList();
            var results = new List<SlotMatch>();

            foreach (var slot in computedSlots)
            {
                var match = candidateList
                    .Where(c => !claimed.Contains(c.Id)
                        && Math.Abs((c.ScheduledFor - slot).TotalMinutes) <= MarScheduleCalculator.MatchTolerance.TotalMinutes)
                    .OrderBy(c => Math.Abs((c.ScheduledFor - slot).TotalMinutes))
                    .ThenByDescending(c => c.ActedAt)
                    .Select(c => (Candidate?)c)
                    .FirstOrDefault();

                if (match.HasValue)
                {
                    claimed.Add(match.Value.Id);
                    results.Add(new SlotMatch(slot, match.Value.Id));
                }
                else
                {
                    results.Add(new SlotMatch(slot, null));
                }
            }

            return results;
        }

        /// <summary>Ids of every candidate claimed by some slot -- the complement identifies ad-hoc (SOS/PRN/extra) doses.</summary>
        public static HashSet<Guid> GetClaimedIds(IEnumerable<SlotMatch> matches) =>
            matches.Where(m => m.MatchedId.HasValue).Select(m => m.MatchedId!.Value).ToHashSet();
    }
}
