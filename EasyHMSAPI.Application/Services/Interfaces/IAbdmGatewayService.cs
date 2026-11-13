namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>Manages the ABDM gateway session (client_credentials) access token used to call
    /// every ABHA V3 API — fetched once and cached until near expiry.</summary>
    public interface IAbdmGatewayService
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

        /// <summary>Drops the cached token so the next call fetches a fresh one — used after ABDM
        /// answers 401, since it can revoke a token before its stated expiry.</summary>
        void InvalidateAccessToken();
    }
}
