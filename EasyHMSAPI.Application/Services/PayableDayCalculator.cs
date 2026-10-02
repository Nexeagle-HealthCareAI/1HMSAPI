using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Hospital payroll calendar policy (weekly-off days, holidays, and whether each is paid).</summary>
    public sealed record PayrollCalendarPolicy(
        IReadOnlySet<DayOfWeek> WeeklyOffDays,
        bool WeeklyOffPayable,
        bool HolidayPayable,
        IReadOnlySet<DateOnly> Holidays)
    {
        /// <summary>Default when a hospital has configured nothing: Sunday off, offs and holidays paid.</summary>
        public static PayrollCalendarPolicy Default { get; } =
            new(new HashSet<DayOfWeek> { DayOfWeek.Sunday }, true, true, new HashSet<DateOnly>());

        public static IReadOnlySet<DayOfWeek> ParseWeeklyOffDays(string? csv)
        {
            var set = new HashSet<DayOfWeek>();
            foreach (var token in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (token.ToUpperInvariant())
                {
                    case "SUN": set.Add(DayOfWeek.Sunday); break;
                    case "MON": set.Add(DayOfWeek.Monday); break;
                    case "TUE": set.Add(DayOfWeek.Tuesday); break;
                    case "WED": set.Add(DayOfWeek.Wednesday); break;
                    case "THU": set.Add(DayOfWeek.Thursday); break;
                    case "FRI": set.Add(DayOfWeek.Friday); break;
                    case "SAT": set.Add(DayOfWeek.Saturday); break;
                }
            }
            return set;
        }

        public static string FormatWeeklyOffDays(IEnumerable<DayOfWeek> days) =>
            string.Join(",", days.OrderBy(d => ((int)d + 6) % 7).Select(d => d.ToString()[..3].ToUpperInvariant()));

        public static async Task<PayrollCalendarPolicy> LoadAsync(AppDbContext context, Guid hospitalId, PayrollPeriod period, CancellationToken cancellationToken)
        {
            var settings = await context.Set<HrPayrollSettings>().AsNoTracking()
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId, cancellationToken);
            var holidays = await context.Set<HrHoliday>().AsNoTracking()
                .Where(h => h.HospitalId == hospitalId && h.HolidayDate >= period.FirstDay && h.HolidayDate <= period.LastDay)
                .Select(h => h.HolidayDate)
                .ToListAsync(cancellationToken);

            return settings == null
                ? Default with { Holidays = holidays.ToHashSet() }
                : new PayrollCalendarPolicy(ParseWeeklyOffDays(settings.WeeklyOffDays), settings.WeeklyOffPayable, settings.HolidayPayable, holidays.ToHashSet());
        }
    }

    /// <summary>
    /// Pure payable-day computation for a salaried employee.
    /// Worked / approved-leave days count 1 (half-day 0.5); weekly offs and declared holidays count 1
    /// when the hospital's policy says they are payable (the default), never before the joining date.
    /// </summary>
    public static class PayableDayCalculator
    {
        public static decimal Compute(
            PayrollPeriod period,
            DateOnly joiningDate,
            IEnumerable<(DateOnly Date, string Status)> attendance,
            PayrollCalendarPolicy policy)
        {
            var byDate = attendance
                .GroupBy(a => a.Date)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Status).ToList());

            decimal total = 0m;
            var start = joiningDate > period.FirstDay ? joiningDate : period.FirstDay;
            for (var d = start; d <= period.LastDay; d = d.AddDays(1))
            {
                decimal value = 0m;
                if (byDate.TryGetValue(d, out var statuses))
                {
                    foreach (var s in statuses)
                    {
                        var v = s switch
                        {
                            "PRESENT" or "LATE" or "ON_LEAVE" => 1m,
                            "HALF_DAY" => 0.5m,
                            _ => 0m,
                        };
                        if (v > value) value = v;
                    }
                }

                var paidOff =
                    (policy.WeeklyOffPayable && policy.WeeklyOffDays.Contains(d.DayOfWeek)) ||
                    (policy.HolidayPayable && policy.Holidays.Contains(d));
                if (paidOff && value < 1m) value = 1m;

                total += value;
            }
            return total;
        }
    }
}
