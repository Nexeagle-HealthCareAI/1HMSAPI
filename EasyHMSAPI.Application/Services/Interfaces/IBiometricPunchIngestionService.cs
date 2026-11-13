using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>One scan as reported by a device, before it is stored.</summary>
    public sealed record IncomingPunch(string DeviceUserId, DateTime PunchTime, int? StateCode = null, int? VerifyType = null, string? RawLine = null);

    public sealed class PunchIngestionResult
    {
        /// <summary>Records in the batch.</summary>
        public int Received { get; set; }
        /// <summary>New scans stored.</summary>
        public int Accepted { get; set; }
        /// <summary>Scans already stored (device re-sent them). Harmless.</summary>
        public int Duplicates { get; set; }
        /// <summary>Unusable records (no PIN, or a timestamp that cannot be right such as a dead-battery 2000-01-01).</summary>
        public int Invalid { get; set; }
        /// <summary>Stored, but the PIN isn't linked to an employee yet; attendance is built once it is mapped.</summary>
        public int Unmapped { get; set; }
        /// <summary>Attendance days created or refreshed from these scans.</summary>
        public int AttendanceDaysUpdated { get; set; }
    }

    public interface IBiometricPunchIngestionService
    {
        /// <summary>
        /// Stores the scans (idempotently), links them to employees via the hospital's PIN mapping, and
        /// rebuilds the affected employees' attendance from the raw scans. Also records the device as seen.
        /// </summary>
        Task<PunchIngestionResult> IngestAsync(HrBiometricDevice device, IReadOnlyList<IncomingPunch> punches, string? remoteIp, CancellationToken cancellationToken);

        /// <summary>After a PIN is mapped to an employee: link that PIN's earlier scans and rebuild attendance. Returns days updated.</summary>
        Task<int> LinkPinAndRebuildAsync(Guid hospitalId, string deviceUserId, Guid employeeId, CancellationToken cancellationToken);

        /// <summary>Rebuilds one employee's attendance for a date range from their raw scans. Returns days created or refreshed.</summary>
        Task<int> RebuildAsync(Guid hospitalId, Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    }
}
