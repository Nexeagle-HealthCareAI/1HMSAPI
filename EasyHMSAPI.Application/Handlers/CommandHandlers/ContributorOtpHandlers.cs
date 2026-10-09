using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    internal static class ContributorLinkCheck
    {
        /// <summary>The link for a token, or an error (404 unknown, 410 used / expired / revoked).</summary>
        public static async Task<(HealthArticleAccessLink? Link, int Status, string? Message)> Resolve(AppDbContext context, string? token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token)) return (null, 404, "This link is not valid.");
            var hash = ContributorSecurity.HashToken(token.Trim());
            var link = await context.HealthArticleAccessLinks.FirstOrDefaultAsync(l => l.TokenHash == hash, ct);
            if (link == null) return (null, 404, "This link is not valid.");
            if (link.RevokedAt != null) return (null, 410, "This link is no longer valid. Ask the NexEagle team to send a new one.");
            if (link.UsedAt != null) return (null, 410, "This link has already been used. Sign in with your mobile number instead.");
            if (link.ExpiresAt <= DateTime.UtcNow) return (null, 410, "This link has expired. Ask the NexEagle team to send a new one.");
            return (link, 200, null);
        }
    }

    /// <summary>
    /// Sends a WhatsApp code to a contributor. With an invitation link the code goes only to the number the link was sent to.
    /// Limits (on top of the per-IP limit on the endpoint): 60 s between codes and 5 codes per number per day; a new code
    /// cancels the previous one. The reply never says whether the number is already registered.
    /// </summary>
    public class ContributorOtpSendHandler : IRequestHandler<ContributorOtpSendRequestModel, ContributorOtpSendResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly IConfiguration _configuration;

        public ContributorOtpSendHandler(AppDbContext context, IWhatsAppMessagingService whatsApp, IConfiguration configuration)
        {
            _context = context;
            _whatsApp = whatsApp;
            _configuration = configuration;
        }

        public async Task<ContributorOtpSendResponseModel> Handle(ContributorOtpSendRequestModel request, CancellationToken ct)
        {
            var mobile = ContributorSecurity.NormalizeMobile(request.Mobile);
            if (mobile == null) return Fail(400, "Enter a 10-digit mobile number.");

            Guid? linkId = null;
            if (!string.IsNullOrWhiteSpace(request.InviteToken))
            {
                var (link, _, _) = await ContributorLinkCheck.Resolve(_context, request.InviteToken, ct);
                if (link == null) return Fail(410, "This link is no longer valid.");
                if (link.Mobile != mobile) return Fail(403, "This link was sent to a different number.");
                linkId = link.LinkId;
            }

            var now = DateTime.UtcNow;
            var dayAgo = now.AddHours(-24);
            var recent = await _context.ContributorOtps.Where(o => o.Mobile == mobile && o.CreatedAt > dayAgo)
                .OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

            if (recent.Count > 0)
            {
                var waited = now - recent[0].CreatedAt;
                if (waited < ContributorSecurity.OtpResendCooldown)
                {
                    var wait = (int)Math.Ceiling((ContributorSecurity.OtpResendCooldown - waited).TotalSeconds);
                    return new ContributorOtpSendResponseModel { Success = false, StatusCode = 429, RetryAfterSeconds = wait, Message = $"Wait {wait} seconds before asking for another code." };
                }
            }
            if (recent.Count >= ContributorSecurity.MaxOtpPerDay)
                return new ContributorOtpSendResponseModel { Success = false, StatusCode = 429, Message = "Too many codes were requested for this number today. Try again tomorrow." };

            // Only the newest code works.
            foreach (var o in recent.Where(o => o.ConsumedAt == null && o.ExpiresAt > now)) o.ConsumedAt = now;

            var secret = OtpSecret(_configuration);
            if (secret == null) return Fail(503, "Sign-in is not available right now.");
            var code = ContributorSecurity.NewOtpCode();
            _context.ContributorOtps.Add(new ContributorOtp
            {
                OtpId = Guid.NewGuid(),
                Mobile = mobile,
                CodeHash = ContributorSecurity.HashOtp(mobile, code, secret),
                LinkId = linkId,
                ExpiresAt = now + ContributorSecurity.OtpLifetime,
                RequestIp = request.RequestIp is { Length: > 45 } ip ? ip[..45] : request.RequestIp,
                CreatedAt = now,
            });

            // Housekeeping for this number only: codes older than two days are of no use to anyone.
            var old = await _context.ContributorOtps.Where(o => o.Mobile == mobile && o.CreatedAt < now.AddDays(-2)).ToListAsync(ct);
            _context.ContributorOtps.RemoveRange(old);
            await _context.SaveChangesAsync(ct);

            var sent = await _whatsApp.SendOtpAsync(mobile, code);
            return sent
                ? new ContributorOtpSendResponseModel { Success = true, StatusCode = 200 }
                : Fail(502, "We could not send the code on WhatsApp. Please try again in a minute.");
        }

        internal static string? OtpSecret(IConfiguration configuration)
        {
            var key = configuration["Jwt:SecretKey"];
            return string.IsNullOrWhiteSpace(key) || key.StartsWith("<") ? null : key;
        }

        private static ContributorOtpSendResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    /// <summary>
    /// Checks the code and starts a session. A person who already has a contributor row (found by number, or the one the
    /// invitation link was made for) is signed in to it; otherwise one is created for the role they chose. The link
    /// is marked used. Wrong codes count against the code (5 tries), and every failure reads the same.
    /// </summary>
    public class ContributorOtpVerifyHandler : IRequestHandler<ContributorOtpVerifyRequestModel, ContributorOtpVerifyResponseModel>
    {
        private static readonly string[] EnrolRoles =
        {
            HealthWikiContributor.TypeIndependentDoctor, HealthWikiContributor.TypeHealthWorker, HealthWikiContributor.TypeWriter,
        };

        private const string WrongCode = "That code is wrong or has expired. Request a new one.";

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public ContributorOtpVerifyHandler(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<ContributorOtpVerifyResponseModel> Handle(ContributorOtpVerifyRequestModel request, CancellationToken ct)
        {
            var mobile = ContributorSecurity.NormalizeMobile(request.Mobile);
            var code = new string((request.Code ?? string.Empty).Where(char.IsDigit).ToArray());
            if (mobile == null || code.Length != ContributorSecurity.OtpLength) return Fail(401, WrongCode);

            var secret = ContributorOtpSendHandler.OtpSecret(_configuration);
            if (secret == null) return Fail(503, "Sign-in is not available right now.");

            var now = DateTime.UtcNow;
            var otp = await _context.ContributorOtps
                .Where(o => o.Mobile == mobile && o.ConsumedAt == null && o.ExpiresAt > now)
                .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
            if (otp == null) return Fail(401, WrongCode);

            if (otp.Attempts >= ContributorSecurity.MaxOtpAttempts)
            {
                otp.ConsumedAt = now;
                await _context.SaveChangesAsync(ct);
                return Fail(401, WrongCode);
            }
            if (!ContributorSecurity.OtpMatches(otp.CodeHash, mobile, code, secret))
            {
                otp.Attempts++;
                if (otp.Attempts >= ContributorSecurity.MaxOtpAttempts) otp.ConsumedAt = now;
                await _context.SaveChangesAsync(ct);
                return Fail(401, WrongCode);
            }

            HealthArticleAccessLink? link = null;
            if (!string.IsNullOrWhiteSpace(request.InviteToken))
            {
                var (found, status, message) = await ContributorLinkCheck.Resolve(_context, request.InviteToken, ct);
                if (found == null) return Fail(status, message!);
                if (found.Mobile != mobile) return Fail(403, "This link was sent to a different number.");
                link = found;
            }

            var contributor = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.Mobile == mobile, ct);
            if (contributor == null && link?.ContributorId != null)
                contributor = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.ContributorId == link.ContributorId, ct);

            if (contributor == null)
            {
                var role = request.Role?.Trim().ToUpperInvariant();
                if (role == null || !EnrolRoles.Contains(role)) return Fail(400, "Choose how you want to join first.");
                contributor = new HealthWikiContributor
                {
                    ContributorId = Guid.NewGuid(), Type = role, FullName = string.Empty, Mobile = mobile,
                    Status = HealthWikiContributor.StatusInvited, EnrolmentSource = "SELF_ENROLLED", CreatedAt = now, UpdatedAt = now,
                };
                _context.HealthWikiContributors.Add(contributor);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // The same number joined twice at once: use the row that won.
                    _context.Entry(contributor).State = EntityState.Detached;
                    contributor = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.Mobile == mobile, ct);
                    if (contributor == null) return Fail(500, "Something went wrong. Try again.");
                }
            }

            if (contributor.Status == HealthWikiContributor.StatusRejected)
                return Fail(403, "This account cannot sign in. Please contact the NexEagle team.");
            if (link != null && link.ContributorId != null && link.ContributorId != contributor.ContributorId)
                return Fail(403, "This link was sent to a different number.");

            otp.ConsumedAt = now;
            if (link != null) link.UsedAt = now;

            var token = ContributorSecurity.NewToken();
            var expires = now + ContributorSecurity.SessionLifetime;
            _context.ContributorSessions.Add(new ContributorSession
            {
                SessionId = Guid.NewGuid(), ContributorId = contributor.ContributorId, TokenHash = ContributorSecurity.HashToken(token),
                ExpiresAt = expires, LastSeenAt = now, CreatedAt = now,
            });
            await _context.SaveChangesAsync(ct);

            return new ContributorOtpVerifyResponseModel
            {
                Success = true, StatusCode = 200, SessionToken = token, SessionExpiresAt = expires,
                NeedsRegistration = string.IsNullOrWhiteSpace(contributor.FullName) || contributor.Status == HealthWikiContributor.StatusInvited,
            };
        }

        private static ContributorOtpVerifyResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    public class ContributorLogoutHandler : IRequestHandler<ContributorLogoutRequestModel, ContributorSimpleResponseModel>
    {
        private readonly AppDbContext _context;

        public ContributorLogoutHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContributorSimpleResponseModel> Handle(ContributorLogoutRequestModel request, CancellationToken ct)
        {
            var session = await _context.ContributorSessions.FirstOrDefaultAsync(s => s.SessionId == request.SessionId, ct);
            if (session != null && session.RevokedAt == null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            return new ContributorSimpleResponseModel { Success = true };
        }
    }
}
