using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Entities;

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

        /// <summary>
        /// The contributor who owns a mobile number that was just proven on Doctor Dekho by a patient sign-in (a WhatsApp code). Only someone
        /// who finished joining (pending or verified) counts; a person who has not joined, or was rejected, is not found.
        /// </summary>
        Task<HealthWikiContributor?> FindByVerifiedMobileAsync(string? mobile, CancellationToken cancellationToken);

        /// <summary>The same, for showing a tag on the person's own profile page: someone the team invited who has not finished their profile is
        /// included (so the page can ask them to finish). A rejected contributor is not.</summary>
        Task<HealthWikiContributor?> FindForProfileAsync(string? mobile, CancellationToken cancellationToken);

        /// <summary>
        /// A contributor session for a signed-in patient, so Health Wiki never asks for a second code for the same number. The number is the one
        /// the patient sign-in already proved. Someone who is already a contributor (even one the team invited and who has not finished) gets a
        /// session for their row; someone who is not gets a new row only when they say which role they are joining as (joinAs). Null when there is
        /// no such contributor and no role was given, or when the contributor was rejected.
        /// </summary>
        Task<HealthWikiPatientSignIn?> SignInPatientAsync(string? mobile, string? joinAs, CancellationToken cancellationToken);
    }
}
