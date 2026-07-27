using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Shared TimeOff > Override > Template precedence for "is this doctor working on date X" —
    /// used both by GetPublicDoctorAvailabilityHandler (single doctor+date) and batched across a
    /// page of doctors for the public directory's "available today" card badge
    /// (GetPublicDoctorsHandler). DoctorShiftTemplate is global (no per-doctor row), so a doctor
    /// with no TimeOff and no Override for a date is available whenever any active template exists.
    /// </summary>
    public static class DoctorAvailabilityResolver
    {
        public static bool HasTimeOffOnDate(DateTime date, IEnumerable<DoctorTimeOff> timeOffsForDoctor)
        {
            var d = date.Date;
            return timeOffsForDoctor.Any(to => d >= to.FromDate.Date && d <= to.ToDate.Date);
        }

        public static List<PublicShiftInfo> ResolveShifts(
            IReadOnlyCollection<DoctorShiftOverride> overridesForDate,
            IReadOnlyCollection<DoctorShiftTemplate> activeTemplates)
        {
            if (overridesForDate.Count > 0)
            {
                return overridesForDate
                    .OrderBy(s => s.StartTime)
                    .Select(s => new PublicShiftInfo { Name = s.ShiftName, StartTime = s.StartTime, EndTime = s.EndTime })
                    .ToList();
            }

            return activeTemplates
                .OrderBy(t => t.StartTime)
                .Select(t => new PublicShiftInfo { Name = t.ShiftName, StartTime = t.StartTime, EndTime = t.EndTime })
                .ToList();
        }

        public static bool IsAvailable(
            DateTime date,
            IEnumerable<DoctorTimeOff> timeOffsForDoctor,
            IReadOnlyCollection<DoctorShiftOverride> overridesForDate,
            IReadOnlyCollection<DoctorShiftTemplate> activeTemplates)
        {
            if (HasTimeOffOnDate(date, timeOffsForDoctor))
                return false;

            return overridesForDate.Count > 0 || activeTemplates.Count > 0;
        }
    }
}
