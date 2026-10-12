using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// Passes a hospital doctor from EasyHMS to Doctor Dekho already signed in. The doctor is identified by EasyHMS (the staff login), so no
    /// WhatsApp code is needed. A short-lived, single-use code is made for them; Doctor Dekho's server swaps it for a normal contributor session.
    /// </summary>
    public interface IHealthWikiHandoffService
    {
        /// <summary>A new code for the doctor's contributor row, or null when that row may not use Doctor Dekho (not a hospital doctor, or rejected).</summary>
        Task<string?> CreateAsync(Guid contributorId, CancellationToken cancellationToken);

        /// <summary>Exchanges the code for a session. A code works once, within a minute; every failure reads the same.</summary>
        Task<HealthWikiHandoffSession?> ExchangeAsync(string? code, CancellationToken cancellationToken);
    }
}
