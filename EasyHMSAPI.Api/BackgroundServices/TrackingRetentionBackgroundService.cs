using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // Deletes website-tracking rows older than the retention period, once a day (decided 2026-10-19):
    //   WebsiteVisits, AnalyticsEvents  -> Tracking:Retention:VisitsAndEventsDays   (default 365)
    //   HospitalLeads                   -> Tracking:Retention:LeadsMonths           (default 24)
    // Deletion is permanent. It runs in batches (short locks on busy tables) and can be switched off
    // from the CMS Night Jobs panel: the JobSettings row 'TrackingDataRetention' (seeded by
    // 1HMSDatabase migration tracking_privacy_2026_10.sql) gates every run, and a missing row means
    // the job stays off. Each run updates that row and writes a NightJobRuns audit row.
    public class TrackingRetentionBackgroundService : BackgroundService
    {
        public const string JobName = "TrackingDataRetention";
        private const int BatchSize = 5000;
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TrackingRetentionBackgroundService> _logger;

        public TrackingRetentionBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
            ILogger<TrackingRetentionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Job} run failed", JobName);
                }

                try { await Task.Delay(RunInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var active = await db.Database
                .SqlQuery<bool>($"SELECT IsActive AS Value FROM dbo.JobSettings WHERE JobName = {JobName}")
                .ToListAsync(ct);
            if (active.Count == 0 || !active[0])
            {
                _logger.LogInformation("{Job} skipped: JobSettings row missing or switched off", JobName);
                return;
            }

            var days = Math.Max(30, _configuration.GetValue("Tracking:Retention:VisitsAndEventsDays", 365));
            var months = Math.Max(1, _configuration.GetValue("Tracking:Retention:LeadsMonths", 24));
            var startedAt = DateTime.UtcNow;
            var eventsCutoff = startedAt.AddDays(-days);
            var leadsCutoff = startedAt.AddMonths(-months);

            string status = "Success", summary, error = "";
            long visits = 0, events = 0, leads = 0;
            try
            {
                visits = await DeleteInBatchesAsync(db, "WebsiteVisits", "VisitedAt", eventsCutoff, ct);
                events = await DeleteInBatchesAsync(db, "AnalyticsEvents", "OccurredAt", eventsCutoff, ct);
                leads = await DeleteInBatchesAsync(db, "HospitalLeads", "OccurredAt", leadsCutoff, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                status = "Failed";
                error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                _logger.LogError(ex, "{Job} failed part-way", JobName);
            }
            summary = $"{JobName}: WebsiteVisits={visits}; AnalyticsEvents={events} (older than {days} days); HospitalLeads={leads} (older than {months} months)";
            _logger.LogInformation("{Summary}", summary);

            var completedAt = DateTime.UtcNow;
            var seconds = (int)(completedAt - startedAt).TotalSeconds;
            var machine = Environment.MachineName;
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "";
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.JobSettings SET LastExecutionDateUTC = {completedAt}, UpdatedAtUtc = SYSUTCDATETIME() WHERE JobName = {JobName};
INSERT INTO dbo.NightJobRuns (StartedAtUtc, CompletedAtUtc, DurationSeconds, Status, MachineName, Environment, Summary, Error)
VALUES ({startedAt}, {completedAt}, {seconds}, {status}, {machine}, {environment}, {summary}, {(error.Length > 0 ? error : null)});", ct);
        }

        // Table and column names are compile-time constants from this class, never user input.
        private static async Task<long> DeleteInBatchesAsync(AppDbContext db, string table, string column, DateTime cutoff, CancellationToken ct)
        {
            long total = 0;
            var sql = $"DELETE TOP ({BatchSize}) FROM dbo.[{table}] WHERE [{column}] < @cutoff";
            while (!ct.IsCancellationRequested)
            {
                var deleted = await db.Database.ExecuteSqlRawAsync(sql,
                    new[] { new Microsoft.Data.SqlClient.SqlParameter("@cutoff", cutoff) }, ct);
                total += deleted;
                if (deleted < BatchSize) break;
            }
            return total;
        }
    }
}
