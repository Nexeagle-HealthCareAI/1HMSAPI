using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Stores device scans and derives attendance from them. Attendance is always REBUILT from the raw
    /// scans (see AttendanceEngine) rather than nudged one scan at a time, so a re-sent batch, a batch that
    /// arrives out of order, or a PIN that is mapped later all converge on the same correct result.
    /// </summary>
    public class BiometricPunchIngestionService : IBiometricPunchIngestionService
    {
        // A dead device clock battery reports 2000-01-01 (or 1970); nothing before this is a real scan.
        private static readonly DateTime EarliestPlausibleScan = new(2020, 1, 1);

        private const string SourceBiometric = "BIOMETRIC";
        private const string SourceManualOverride = "MANUAL_OVERRIDE";
        private const string FlagUnscheduled = "UNSCHEDULED";
        private const string FlagMissingIn = "MISSING_IN_PUNCH";

        private readonly AppDbContext _context;

        public BiometricPunchIngestionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PunchIngestionResult> IngestAsync(HrBiometricDevice device, IReadOnlyList<IncomingPunch> punches, string? remoteIp, CancellationToken cancellationToken)
        {
            var result = new PunchIngestionResult { Received = punches.Count };

            // Devices send local wall-clock time; UTC + 2 days is generous enough to cover any timezone plus a
            // slightly fast clock while still rejecting garbage far in the future.
            var latestPlausibleScan = DateTime.UtcNow.AddDays(2);

            // 1. Clean the batch and drop repeats inside it.
            var clean = new Dictionary<(string Pin, DateTime Time), IncomingPunch>();
            foreach (var punch in punches)
            {
                var pin = punch.DeviceUserId?.Trim();
                var time = TrimToSeconds(punch.PunchTime);
                if (string.IsNullOrEmpty(pin) || pin.Length > 50 || time < EarliestPlausibleScan || time > latestPlausibleScan)
                {
                    result.Invalid++;
                    continue;
                }

                if (!clean.TryAdd((pin, time), punch with { DeviceUserId = pin, PunchTime = time }))
                {
                    result.Duplicates++;
                }
            }

            var affected = new List<(Guid EmployeeId, DateOnly Date)>();

            if (clean.Count > 0)
            {
                var pins = clean.Keys.Select(k => k.Pin).Distinct().ToList();
                var earliest = clean.Keys.Min(k => k.Time);
                var latest = clean.Keys.Max(k => k.Time);

                // 2. Skip scans this device has already delivered (a re-sent batch must never double-count).
                var already = await _context.HrBiometricPunch
                    .Where(p => p.HrBiometricDeviceId == device.HrBiometricDeviceId
                             && pins.Contains(p.DeviceUserId)
                             && p.PunchTime >= earliest && p.PunchTime <= latest)
                    .Select(p => new { p.DeviceUserId, p.PunchTime })
                    .ToListAsync(cancellationToken);
                var alreadyStored = already.Select(a => (a.DeviceUserId, a.PunchTime)).ToHashSet();

                // 3. PIN -> employee, within THIS hospital only (different hospitals reuse the same PINs).
                var pinToEmployee = await _context.HrEmployeeDeviceUser
                    .Where(m => m.HospitalId == device.HospitalId && pins.Contains(m.DeviceUserId))
                    .ToDictionaryAsync(m => m.DeviceUserId, m => m.HrEmployeeId, cancellationToken);

                foreach (var ((pin, time), punch) in clean)
                {
                    if (alreadyStored.Contains((pin, time)))
                    {
                        result.Duplicates++;
                        continue;
                    }

                    Guid? employeeId = pinToEmployee.TryGetValue(pin, out var mapped) ? mapped : null;

                    _context.HrBiometricPunch.Add(new HrBiometricPunch
                    {
                        HospitalId = device.HospitalId,
                        HrBiometricDeviceId = device.HrBiometricDeviceId,
                        DeviceUserId = pin,
                        HrEmployeeId = employeeId,
                        PunchTime = time,
                        StateCode = punch.StateCode,
                        VerifyType = punch.VerifyType,
                        RawLine = punch.RawLine is { Length: > 500 } ? punch.RawLine[..500] : punch.RawLine
                    });

                    result.Accepted++;
                    if (employeeId.HasValue) affected.Add((employeeId.Value, DateOnly.FromDateTime(time)));
                    else result.Unmapped++;

                    if (!device.LastPunchTime.HasValue || time > device.LastPunchTime.Value) device.LastPunchTime = time;
                }
            }

            device.LastSeenAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(remoteIp)) device.LastSeenIp = remoteIp.Length > 64 ? remoteIp[..64] : remoteIp;

            // If two overlapping deliveries of the same scans race, the unique index rejects the loser and
            // it fails; devices retry, and the retry is deduplicated above. No data is lost or doubled.
            await _context.SaveChangesAsync(cancellationToken);

            // 4. Rebuild each affected employee's attendance from their raw scans.
            foreach (var employee in affected.GroupBy(a => a.EmployeeId))
            {
                result.AttendanceDaysUpdated += await RebuildAsync(
                    device.HospitalId, employee.Key, employee.Min(a => a.Date), employee.Max(a => a.Date), cancellationToken);
            }

            return result;
        }

        public async Task<int> LinkPinAndRebuildAsync(Guid hospitalId, string deviceUserId, Guid employeeId, CancellationToken cancellationToken)
        {
            var unlinked = await _context.HrBiometricPunch
                .Where(p => p.HospitalId == hospitalId && p.DeviceUserId == deviceUserId && p.HrEmployeeId == null)
                .ToListAsync(cancellationToken);
            if (unlinked.Count == 0) return 0;

            foreach (var punch in unlinked) punch.HrEmployeeId = employeeId;
            await _context.SaveChangesAsync(cancellationToken);

            return await RebuildAsync(
                hospitalId, employeeId,
                DateOnly.FromDateTime(unlinked.Min(p => p.PunchTime)),
                DateOnly.FromDateTime(unlinked.Max(p => p.PunchTime)),
                cancellationToken);
        }

        public async Task<int> RebuildAsync(Guid hospitalId, Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            // A night shift that starts on the last day of the range ends on the next one, and one that began the
            // day before the range ends inside it, so read a day either side and only WRITE days inside the range.
            var windowStart = from.AddDays(-1).ToDateTime(TimeOnly.MinValue);
            var windowEnd = to.AddDays(2).ToDateTime(TimeOnly.MinValue);

            var scans = await _context.HrBiometricPunch
                .Where(p => p.HospitalId == hospitalId && p.HrEmployeeId == employeeId
                         && p.PunchTime >= windowStart && p.PunchTime < windowEnd)
                .OrderBy(p => p.PunchTime)
                .Select(p => new { p.PunchTime, p.HrBiometricDeviceId })
                .ToListAsync(cancellationToken);
            if (scans.Count == 0) return 0;

            var roster = await _context.HrDutyRoster
                .Include(r => r.HrHospitalShift)
                .Where(r => r.HrEmployeeId == employeeId && r.RosterDate >= from.AddDays(-1) && r.RosterDate <= to.AddDays(1))
                .ToListAsync(cancellationToken);
            var shifts = roster.Select(r => new RosteredShift(
                r.RosterDate, r.HrHospitalShift.ShiftCode, r.HrHospitalShift.StartTime, r.HrHospitalShift.EndTime, r.HrHospitalShift.GracePeriodMinutes));

            var days = AttendanceEngine.Compute(scans.Select(s => s.PunchTime), shifts)
                .Where(d => d.WorkDate >= from && d.WorkDate <= to)
                .ToList();
            if (days.Count == 0) return 0;

            var existing = await _context.HrAttendanceLog
                .Where(a => a.HrEmployeeId == employeeId && a.AttendanceDate >= from && a.AttendanceDate <= to)
                .ToListAsync(cancellationToken);
            var deviceSerials = await _context.HrBiometricDevice
                .Where(d => d.HospitalId == hospitalId)
                .ToDictionaryAsync(d => d.HrBiometricDeviceId, d => d.SerialNumber, cancellationToken);

            var updated = 0;
            foreach (var day in days)
            {
                var log = existing.FirstOrDefault(a => a.AttendanceDate == day.WorkDate);

                // A person's manual correction (or approved leave) is a decision; a device scan must not undo it.
                if (log != null && (log.PunchSource == SourceManualOverride || log.Status == "ON_LEAVE")) continue;

                if (log == null)
                {
                    log = new HrAttendanceLog { HrEmployeeId = employeeId, AttendanceDate = day.WorkDate };
                    _context.HrAttendanceLog.Add(log);
                }

                var firstScan = scans.First(s => s.PunchTime == day.PunchIn);
                log.PunchIn = day.PunchIn;
                log.PunchOut = day.PunchOut;
                log.TotalHoursWorked = day.TotalHours;
                log.OvertimeHours = day.OvertimeHours;
                log.Status = day.Status;
                log.PunchSource = SourceBiometric;
                log.BiometricDeviceId = deviceSerials.TryGetValue(firstScan.HrBiometricDeviceId, out var serial) ? serial : null;

                // Notes doubles as the exception flag the attendance-exceptions view reads. Only touch it when it
                // is empty or one of our own flags, so a person's typed note is never overwritten.
                if (day.Unscheduled)
                {
                    if (string.IsNullOrEmpty(log.Notes) || log.Notes == FlagMissingIn) log.Notes = FlagUnscheduled;
                }
                else if (log.Notes == FlagUnscheduled || log.Notes == FlagMissingIn)
                {
                    log.Notes = null;
                }

                updated++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return updated;
        }

        private static DateTime TrimToSeconds(DateTime value) =>
            new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified);
    }
}
