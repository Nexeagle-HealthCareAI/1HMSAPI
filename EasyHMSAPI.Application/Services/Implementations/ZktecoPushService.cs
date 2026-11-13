using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// UNVERIFIED against a physical device: this follows the published ZKTeco push (ADMS) protocol as
    /// documented, but no K40 Pro has connected to it yet. It is built to be corrected quickly from real
    /// traffic: every scan keeps its raw line, an unreadable batch is logged with a preview, an unregistered
    /// serial is logged with the caller's IP (so an admin can find and register it), and calls to endpoints
    /// this doesn't implement are logged instead of silently 404ing.
    /// </summary>
    public class ZktecoPushService : IZktecoPushService
    {
        private const string Rejected = "ERROR: device not registered";

        private readonly AppDbContext _context;
        private readonly IBiometricPunchIngestionService _ingestion;
        private readonly ILogger<ZktecoPushService> _logger;

        public ZktecoPushService(AppDbContext context, IBiometricPunchIngestionService ingestion, ILogger<ZktecoPushService> logger)
        {
            _context = context;
            _ingestion = ingestion;
            _logger = logger;
        }

        public async Task<ZktecoPushResult> HandshakeAsync(string? serial, string? remoteIp, CancellationToken cancellationToken)
        {
            var device = await FindActiveDeviceAsync(serial, remoteIp, cancellationToken);
            if (device == null) return new ZktecoPushResult(403, Rejected);

            device.LastSeenAt = DateTime.UtcNow;
            device.LastSeenIp = Clip(remoteIp, 64);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ZKTeco handshake from device {Serial} ({Name}) at {Ip}.", device.SerialNumber, device.Name, remoteIp);

            // ATTLOGStamp tells the device where to resume from, so one that was offline doesn't re-send its
            // whole history on every start-up. (Re-sent scans are harmless anyway: they are de-duplicated.)
            var stamp = string.IsNullOrWhiteSpace(device.LastAttlogStamp) ? "None" : device.LastAttlogStamp;
            var options = string.Join("\n", new[]
            {
                $"GET OPTION FROM: {device.SerialNumber}",
                $"ATTLOGStamp={stamp}",
                "OPERLOGStamp=9999",
                "ATTPHOTOStamp=None",
                "ErrorDelay=30",
                "Delay=10",
                "TransTimes=00:00;14:05",
                "TransInterval=1",
                "TransFlag=TransData AttLog",
                "Realtime=1",
                "Encrypt=0",
            }) + "\n";

            return new ZktecoPushResult(200, options);
        }

        public async Task<ZktecoPushResult> UploadAsync(string? serial, string? table, string? stamp, string? body, string? remoteIp, CancellationToken cancellationToken)
        {
            var device = await FindActiveDeviceAsync(serial, remoteIp, cancellationToken);
            if (device == null) return new ZktecoPushResult(403, Rejected);

            // Only attendance logs matter here. Operation logs, user records, photos and the like are
            // acknowledged so the device moves on, then ignored (fingerprint templates are never stored).
            if (!string.Equals(table, "ATTLOG", StringComparison.OrdinalIgnoreCase))
            {
                device.LastSeenAt = DateTime.UtcNow;
                device.LastSeenIp = Clip(remoteIp, 64);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("ZKTeco device {Serial} sent table {Table}; acknowledged and ignored.", device.SerialNumber, table);
                return new ZktecoPushResult(200, "OK");
            }

            var parsed = ZktecoAttlogParser.Parse(body);

            if (parsed.Skipped > 0 || (parsed.Punches.Count == 0 && !string.IsNullOrWhiteSpace(body)))
            {
                // The one place a real device is likely to surprise us, so keep enough to fix the parser.
                _logger.LogWarning(
                    "ZKTeco device {Serial}: {Skipped} unreadable line(s), {Parsed} read. Body preview: {Preview}",
                    device.SerialNumber, parsed.Skipped, parsed.Punches.Count, Clip(body, 300));
            }

            if (!string.IsNullOrWhiteSpace(stamp)) device.LastAttlogStamp = Clip(stamp.Trim(), 50);

            var result = await _ingestion.IngestAsync(device, parsed.Punches.ToList(), remoteIp, cancellationToken);

            _logger.LogInformation(
                "ZKTeco device {Serial}: {Received} scan(s) received, {Accepted} new, {Duplicates} already stored, {Unmapped} for unlinked IDs.",
                device.SerialNumber, result.Received, result.Accepted, result.Duplicates, result.Unmapped);

            return new ZktecoPushResult(200, $"OK:{parsed.Punches.Count}");
        }

        public async Task<ZktecoPushResult> TouchAsync(string? serial, string? remoteIp, CancellationToken cancellationToken)
        {
            var device = await FindActiveDeviceAsync(serial, remoteIp, cancellationToken);
            if (device == null) return new ZktecoPushResult(403, Rejected);

            device.LastSeenAt = DateTime.UtcNow;
            device.LastSeenIp = Clip(remoteIp, 64);
            await _context.SaveChangesAsync(cancellationToken);

            return new ZktecoPushResult(200, "OK");
        }

        private async Task<HrBiometricDevice?> FindActiveDeviceAsync(string? serial, string? remoteIp, CancellationToken cancellationToken)
        {
            var normalized = BiometricDeviceCredentials.NormalizeSerial(serial);
            var device = normalized.Length == 0
                ? null
                : await _context.HrBiometricDevice.FirstOrDefaultAsync(d => d.SerialNumber == normalized, cancellationToken);

            if (device == null || !device.IsActive)
            {
                // Logged (with the IP) so an admin who just plugged a device in can see what serial to register.
                _logger.LogWarning("ZKTeco push from unregistered or inactive device serial '{Serial}' at {Ip}.", serial, remoteIp);
                return null;
            }

            return device;
        }

        private static string? Clip(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
    }
}
