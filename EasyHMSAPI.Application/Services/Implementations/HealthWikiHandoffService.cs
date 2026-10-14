using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiHandoffService : IHealthWikiHandoffService
    {
        private readonly AppDbContext _context;

        public HealthWikiHandoffService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> CreateAsync(Guid contributorId, CancellationToken ct)
        {
            var c = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(x => x.ContributorId == contributorId, ct);
            // Only hospital doctors come this way. Everyone else signs in with a WhatsApp code.
            if (c == null || c.Type != HealthWikiContributor.TypeHospitalDoctor || c.Status == HealthWikiContributor.StatusRejected) return null;

            var now = DateTime.UtcNow;
            var code = ContributorSecurity.NewToken();
            _context.ContributorHandoffs.Add(new ContributorHandoff
            {
                HandoffId = Guid.NewGuid(), ContributorId = c.ContributorId, TokenHash = ContributorSecurity.HashToken(code),
                ExpiresAt = now + ContributorSecurity.HandoffLifetime, CreatedAt = now,
            });
            await _context.SaveChangesAsync(ct);
            return code;
        }

        public async Task<HealthWikiHandoffSession?> ExchangeAsync(string? code, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 200) return null;
            var now = DateTime.UtcNow;
            var hash = ContributorSecurity.HashToken(code.Trim());
            var handoff = await _context.ContributorHandoffs.FirstOrDefaultAsync(h => h.TokenHash == hash && h.UsedAt == null && h.ExpiresAt > now, ct);
            if (handoff == null) return null;

            var c = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(x => x.ContributorId == handoff.ContributorId, ct);
            if (c == null || c.Type != HealthWikiContributor.TypeHospitalDoctor || c.Status == HealthWikiContributor.StatusRejected) return null;

            // Used first, so a second request with the same code finds it spent even if the session below fails to save.
            handoff.UsedAt = now;
            var token = ContributorSecurity.NewToken();
            var expires = now + ContributorSecurity.SessionLifetime;
            _context.ContributorSessions.Add(new ContributorSession
            {
                SessionId = Guid.NewGuid(), ContributorId = c.ContributorId, TokenHash = ContributorSecurity.HashToken(token),
                ExpiresAt = expires, LastSeenAt = now, CreatedAt = now,
            });
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
            return new HealthWikiHandoffSession { Token = token, ExpiresAt = expires };
        }

        public async Task<HealthWikiContributor?> FindByVerifiedMobileAsync(string? mobile, CancellationToken ct)
        {
            var m = ContributorSecurity.NormalizeMobile(mobile);
            if (m == null) return null;
            var c = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(x => x.Mobile == m, ct);
            return c != null && ContributorPortalRules.IsProfileAccepted(c.Status) ? c : null;
        }

        public async Task<HealthWikiHandoffSession?> StartSessionByVerifiedMobileAsync(string? mobile, CancellationToken ct)
        {
            var c = await FindByVerifiedMobileAsync(mobile, ct);
            if (c == null) return null;
            var now = DateTime.UtcNow;
            var token = ContributorSecurity.NewToken();
            var expires = now + ContributorSecurity.SessionLifetime;
            _context.ContributorSessions.Add(new ContributorSession
            {
                SessionId = Guid.NewGuid(), ContributorId = c.ContributorId, TokenHash = ContributorSecurity.HashToken(token),
                ExpiresAt = expires, LastSeenAt = now, CreatedAt = now,
            });
            await _context.SaveChangesAsync(ct);
            return new HealthWikiHandoffSession { Token = token, ExpiresAt = expires };
        }
    }
}
