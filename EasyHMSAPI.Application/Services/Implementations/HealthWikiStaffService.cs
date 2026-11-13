using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiStaffService : IHealthWikiStaffService
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiNotifier _notifier;

        public HealthWikiStaffService(AppDbContext context, IHealthWikiNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<HealthWikiContributor?> EnsureContributorAsync(Guid userId, CancellationToken ct)
        {
            if (userId == Guid.Empty) return null;
            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserID == userId, ct);
            if (doctor == null) return null;

            var now = DateTime.UtcNow;
            var wentLive = new List<HealthArticle>();
            var contributor = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.DoctorId == doctor.DoctorID, ct);
            if (contributor == null)
            {
                contributor = new HealthWikiContributor
                {
                    ContributorId = Guid.NewGuid(), Type = HealthWikiContributor.TypeHospitalDoctor, FullName = "Doctor",
                    Status = HealthWikiContributor.StatusPending, EnrolmentSource = "SELF_ENROLLED", DoctorId = doctor.DoctorID, CreatedAt = now, UpdatedAt = now,
                };
                _context.HealthWikiContributors.Add(contributor);
                await Apply(contributor, doctor, now, wentLive, ct);
                HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, contributor.ContributorId, "JOINED", HealthWikiAudit.ActorDoctorStaff, contributor.FullName);
            }
            else
            {
                await Apply(contributor, doctor, now, wentLive, ct);
            }

            await _context.SaveChangesAsync(ct);
            await TellAsync(wentLive, ct);
            return contributor;
        }

        public async Task SyncHospitalDoctorsAsync(Guid? onlyContributorId, CancellationToken ct)
        {
            var rows = await _context.HealthWikiContributors
                .Where(c => c.DoctorId != null && (onlyContributorId == null || c.ContributorId == onlyContributorId))
                .ToListAsync(ct);
            if (rows.Count == 0) return;

            var doctorIds = rows.Select(c => c.DoctorId!.Value).ToList();
            var doctors = await _context.Doctors.AsNoTracking().Where(d => doctorIds.Contains(d.DoctorID)).ToDictionaryAsync(d => d.DoctorID, ct);
            var now = DateTime.UtcNow;
            var wentLive = new List<HealthArticle>();
            foreach (var c in rows)
                if (doctors.TryGetValue(c.DoctorId!.Value, out var doctor)) await Apply(c, doctor, now, wentLive, ct);
            await _context.SaveChangesAsync(ct);
            await TellAsync(wentLive, ct);
        }

        // Copies the doctor's profile onto the contributor row and makes the status follow Doctors.IsRegistrationVerified.
        // Articles that went live because their reviewer was just verified: their author and reviewer are told, after the change has saved.
        private async Task TellAsync(List<HealthArticle> wentLive, CancellationToken ct)
        {
            foreach (var a in wentLive)
                await _notifier.PublishedAsync(HealthArticleRef.From(a), a.AuthorContributorId, a.ReviewerContributorId, ct);
        }

        private async Task Apply(HealthWikiContributor c, Doctor doctor, DateTime now, List<HealthArticle> wentLive, CancellationToken ct)
        {
            if (c.Status == HealthWikiContributor.StatusRejected) return;

            var name = await _context.UserProfiles.AsNoTracking().Where(u => u.UserID == doctor.UserID).Select(u => u.FullName).FirstOrDefaultAsync(ct);
            var speciality = doctor.PrimaryMedicalSpecialityId == null ? null
                : await _context.MedicalSpecialities.AsNoTracking().Where(m => m.SpecialityId == doctor.PrimaryMedicalSpecialityId)
                    .Select(m => m.PatientFacingName ?? m.Name).FirstOrDefaultAsync(ct);

            // A placeholder licence ("PENDING") is not a registration number; leaving it empty keeps the doctor from being verified on it.
            var number = DoctorProfileRules.IsPlaceholderLicence(doctor.LicenseNumber) ? null : doctor.LicenseNumber.Trim();
            var council = string.IsNullOrWhiteSpace(doctor.MedicalCouncil) ? null : doctor.MedicalCouncil.Trim();

            c.FullName = string.IsNullOrWhiteSpace(name) ? "Doctor" : name.Trim();
            c.Qualification = string.IsNullOrWhiteSpace(doctor.Qualification) ? null : doctor.Qualification.Trim();
            c.Speciality = speciality;
            c.RegistrationNumber = number;
            c.RegistrationCouncil = council;
            c.RegistrationYear = doctor.RegistrationYear;

            var verified = doctor.IsRegistrationVerified && number != null && council != null;
            if (verified && c.Status != HealthWikiContributor.StatusVerified)
            {
                c.Status = HealthWikiContributor.StatusVerified;
                c.VerifiedAt = doctor.RegistrationVerifiedAt ?? now;
                c.VerifiedBy = "CMS (NMC register check)";
                HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, c.ContributorId, "VERIFIED", HealthWikiAudit.ActorSystem, c.VerifiedBy);
                // Articles this doctor approved while their registration was still being checked go live now.
                wentLive.AddRange(await HealthArticlePublisher.PublishWaitingFor(_context, c, c.VerifiedBy, now, ct));
            }
            else if (!verified && c.Status == HealthWikiContributor.StatusVerified)
            {
                // The CMS unmarked the registration, or it changed: back to pending until it is checked again.
                c.Status = HealthWikiContributor.StatusPending;
                c.VerifiedAt = null;
                c.VerifiedBy = null;
                HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, c.ContributorId, "VERIFICATION_REMOVED", HealthWikiAudit.ActorSystem, "System",
                    "The registration is no longer marked as verified");
            }
            if (_context.Entry(c).State == EntityState.Modified) c.UpdatedAt = now;
        }
    }
}
