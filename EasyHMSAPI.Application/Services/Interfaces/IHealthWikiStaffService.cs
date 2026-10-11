using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// Hospital doctors take part in the Health Wiki as contributors linked to their EasyHMS Doctor row. The contributor row is
    /// made the first time the doctor opens the Health Wiki (they opt in by visiting) and is refreshed from their Doctor profile on
    /// every visit. Whether the registration is verified is NOT decided here: EasyHMS already has that, Doctors.IsRegistrationVerified,
    /// which a CMS admin sets after checking the NMC register. The contributor row follows it, so a doctor is verified once, in one place,
    /// and the "Medically reviewed by" badge always means a checked registration.
    /// </summary>
    public interface IHealthWikiStaffService
    {
        /// <summary>The doctor's contributor row, or null when the signed-in user is not a doctor.</summary>
        Task<HealthWikiContributor?> EnsureContributorAsync(Guid userId, CancellationToken cancellationToken);

        /// <summary>
        /// Brings hospital-doctor contributor rows in line with their Doctor profile (all of them, or just one). Articles a doctor approved
        /// while waiting for verification go live when they become verified.
        /// </summary>
        Task SyncHospitalDoctorsAsync(Guid? onlyContributorId, CancellationToken cancellationToken);
    }
}
