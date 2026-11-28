using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // ABDM M2: links each newly saved record to the patient's ABHA without a click (HIP_INTI_LINK_505),
    // and has ABDM text a deep link to a patient who gave a mobile and no ABHA (HIP_INIT_NOTIFY_HIECM).
    // Runs every Abdm:AutoLinkIntervalSeconds (default 60). Abdm:AutoLink / Abdm:RecordSms = false turn each part off.
    public class AbdmAutoLinkBackgroundService : BackgroundService
    {
        private const string IntervalKey = "Abdm:AutoLinkIntervalSeconds";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AbdmAutoLinkBackgroundService> _logger;
        private readonly TimeSpan _interval;

        public AbdmAutoLinkBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AbdmAutoLinkBackgroundService> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(int.TryParse(configuration[IntervalKey], out var seconds) && seconds >= 15 ? seconds : 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IAbdmAutoLinkService>().SweepAsync(DateTime.UtcNow, stoppingToken);
                    if (result.LinksStarted > 0 || result.SmsSent > 0)
                        _logger.LogInformation("ABDM auto-link: {Links} link(s) started, {Sms} record SMS sent.", result.LinksStarted, result.SmsSent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError("ABDM auto-link sweep failed: {Error}.", ex.GetType().Name);
                }

                try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
