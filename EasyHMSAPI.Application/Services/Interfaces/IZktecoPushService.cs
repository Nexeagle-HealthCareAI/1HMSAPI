namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>What to send back to the device: an HTTP status and a plain-text body.</summary>
    public sealed record ZktecoPushResult(int StatusCode, string Body);

    /// <summary>
    /// ZKTeco's "push" (ADMS / iClock) protocol: the terminal calls OUT to a server address configured in its
    /// Cloud Server menu, so it works from behind a hospital's router with no inbound ports. The device
    /// identifies itself only by serial number (the protocol has no token), so a device must be registered
    /// and active before anything it sends is accepted.
    /// </summary>
    public interface IZktecoPushService
    {
        /// <summary>GET /iclock/cdata: the device's start-up handshake. Replies with the options it should run with.</summary>
        Task<ZktecoPushResult> HandshakeAsync(string? serial, string? remoteIp, CancellationToken cancellationToken);

        /// <summary>POST /iclock/cdata?table=...: a batch of records. Attendance logs are stored; other tables are acknowledged and ignored.</summary>
        Task<ZktecoPushResult> UploadAsync(string? serial, string? table, string? stamp, string? body, string? remoteIp, CancellationToken cancellationToken);

        /// <summary>Any other device call (command polling, command results): records that the device is alive and acknowledges.</summary>
        Task<ZktecoPushResult> TouchAsync(string? serial, string? remoteIp, CancellationToken cancellationToken);
    }
}
