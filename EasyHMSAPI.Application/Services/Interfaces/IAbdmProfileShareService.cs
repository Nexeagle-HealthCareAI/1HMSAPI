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

        /// <summary>Links a facility to our bridge (our client ID) as a HIP and an HIU, with ABDM's
        /// facility registry (POST /v1/bridges/MutipleHRPAddUpdateServices). Without it ABDM answers
        /// every M2 call for the facility with ABDM-1035 "Invalid HIP ID". Returns ABDM's body.</summary>
        Task<string> LinkFacilityServicesAsync(string facilityId, string facilityName, string hipName, CancellationToken cancellationToken);
    }
}
