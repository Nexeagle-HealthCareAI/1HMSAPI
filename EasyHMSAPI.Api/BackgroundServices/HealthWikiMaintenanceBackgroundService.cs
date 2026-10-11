using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using MediatR;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // Runs the Health Wiki's daily upkeep (doctor reminders, closing stale topic requests, clearing old sign-in records) once a day, in the same
    // plain timer-loop style as ExpiryAlertBackgroundService. Safe to repeat: reminders are spaced 3 days apart by their own record, a closed
    // request stays closed. Switch off with HealthWiki:MaintenanceEnabled = false. The same work can be run by hand: POST
    // /internal/health-wiki/maintenance/run.
    public class HealthWikiMaintenanceBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthWikiMaintenanceBackgroundService> _logger;

        public HealthWikiMaintenanceBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<HealthWikiMaintenanceBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.Equals(_configuration["HealthWiki:MaintenanceEnabled"]?.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Health Wiki daily upkeep is switched off (HealthWiki:MaintenanceEnabled).");
                return;
            }

            // A short wait after start so it does not compete with the app coming up.
            try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RunHealthWikiMaintenanceRequestModel(), stoppingToken);
                    _logger.LogInformation("Health Wiki daily upkeep: {Sent} reminders sent, {NotSent} not sent, {Closed} topic requests closed, {Otps} codes and {Sessions} sessions cleared.",
                        result.RemindersSent, result.RemindersNotSent, result.TopicsClosed, result.OtpsRemoved, result.SessionsRemoved);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Health Wiki daily upkeep failed.");
                }

                try { await Task.Delay(RunInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
