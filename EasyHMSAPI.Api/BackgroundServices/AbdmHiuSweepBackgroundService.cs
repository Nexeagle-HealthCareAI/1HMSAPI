using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Api.BackgroundServices
{
    // ABDM M3: records received from other facilities may be kept only until their consent's dataEraseAt. Every hour this
    // deletes the ones past it, for every hospital, and closes HIU transfers that received nothing within a day (their
    // private key and data push address are thrown away). Always on: erasing on time is an ABDM rule, not a preference,
    // so unlike the night jobs there is no JobSettings switch.
    public class AbdmHiuSweepBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AbdmHiuSweepBackgroundService> _logger;

        public AbdmHiuSweepBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AbdmHiuSweepBackgroundService> logger)
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
                    var sweep = await scope.ServiceProvider.GetRequiredService<IAbdmHiuService>().SweepAsync(DateTime.UtcNow, stoppingToken);
                    if (sweep.RecordsErased > 0 || sweep.TransfersClosed > 0)
                        _logger.LogInformation("ABDM HIU sweep: {Records} received record(s) erased, {Transfers} transfer(s) closed.", sweep.RecordsErased, sweep.TransfersClosed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError("ABDM HIU sweep failed: {Error}.", ex.GetType().Name);
                }

                try { await Task.Delay(RunInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
