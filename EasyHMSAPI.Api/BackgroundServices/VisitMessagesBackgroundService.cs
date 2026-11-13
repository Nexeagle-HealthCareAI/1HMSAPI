using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // Every 5 minutes: the WhatsApp reminders around a Doctor Dekho visit (morning of the visit, about two hours before) and the
    // after-visit message (Vita vision, Phase 4; see VisitMessages and RunVisitMessagesHandler). Switched on and off from the CMS
    // Night Jobs panel: the JobSettings row 'VisitMessages' (1HMSDatabase alter_appointments_visit_messages.sql) starts OFF, to be
    // switched on once the visit_* WhatsApp templates are approved in Meta. A missing row means off.
    public class VisitMessagesBackgroundService : BackgroundService
    {
        public const string JobName = "VisitMessages";
        private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VisitMessagesBackgroundService> _logger;

        public VisitMessagesBackgroundService(IServiceScopeFactory scopeFactory, ILogger<VisitMessagesBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var active = await db.Database
                        .SqlQuery<bool>($"SELECT IsActive AS Value FROM dbo.JobSettings WHERE JobName = {JobName}")
                        .ToListAsync(stoppingToken);
                    if (active.Count > 0 && active[0])
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new RunVisitMessagesRequestModel { NowUtc = DateTime.UtcNow }, stoppingToken);
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE dbo.JobSettings SET LastExecutionDateUTC = {DateTime.UtcNow}, UpdatedAtUtc = SYSUTCDATETIME() WHERE JobName = {JobName}", stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "{Job} run failed", JobName);
                }
                try { await Task.Delay(RunInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
