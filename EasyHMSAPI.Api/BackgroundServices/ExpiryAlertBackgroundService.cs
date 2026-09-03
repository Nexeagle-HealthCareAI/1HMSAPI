using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // Fires EvaluateExpiryAlertsHandler once a day for every active hospital. This is the only
    // scheduled/background job in the API today — everything else in this codebase (including the
    // admission-alert evaluator this mirrors) runs strictly on-demand from an endpoint call. Kept
    // deliberately small (a plain timer loop, no Hangfire/Quartz dependency) since one daily job is
    // all Phase 3b needs; revisit if a second scheduled job shows up and this should generalize.
    public class ExpiryAlertBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiryAlertBackgroundService> _logger;
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        public ExpiryAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ExpiryAlertBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Small startup delay so this doesn't compete with request handling during app boot.
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExpiryAlertBackgroundService run failed.");
                }

                try { await Task.Delay(RunInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var hospitalIds = await context.Hospitals.AsNoTracking()
                .Where(h => h.IsActive && !h.IsArchived)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            foreach (var hospitalId in hospitalIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    var response = await mediator.Send(new EvaluateExpiryAlertsRequestModel
                    {
                        HospitalId = hospitalId,
                        LoggedInUserName = "ExpiryAlertBackgroundService",
                    }, cancellationToken);

                    if (!response.Success)
                        _logger.LogWarning("Expiry alert evaluation failed for hospitalId {HospitalId}: {Message}", hospitalId, response.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Expiry alert evaluation threw for hospitalId {HospitalId}", hospitalId);
                }
            }
        }
    }
}
