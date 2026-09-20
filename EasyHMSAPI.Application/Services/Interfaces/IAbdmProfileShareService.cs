namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>Outbound ABDM calls for the "Scan Health Facility QR" flow: acknowledging a received
    /// profile share, and registering our public callback (bridge) URL with ABDM.</summary>
    public interface IAbdmProfileShareService
    {
        /// <summary>POSTs the /v3/hip/patient/profile/on-share acknowledgement for a share whose
        /// callback carried <paramref name="callbackRequestId"/>.</summary>
        Task AcknowledgeAsync(string callbackRequestId, string? abhaAddress, bool accepted, string? errorMessage, CancellationToken cancellationToken);

        /// <summary>PATCHes ABDM's bridge URL registration; returns ABDM's response body verbatim.</summary>
        Task<string> RegisterBridgeUrlAsync(string bridgeUrl, CancellationToken cancellationToken);
    }
}
