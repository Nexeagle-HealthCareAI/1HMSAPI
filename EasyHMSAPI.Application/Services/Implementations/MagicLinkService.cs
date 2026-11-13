using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class MagicLinkService : IMagicLinkService
    {
        // Same wording for every failure — see IMagicLinkService.ExchangeAsync.
        public const string InvalidLinkMessage = "This sign-in link is invalid or has expired. Please log in with your mobile number.";

        private const int DefaultLifetimeHours = 24;
        private const int MaxLifetimeHours = 72;
        private const int MaxTargetPathLength = 200;
        private const int PruneBatchSize = 200;
        private static readonly TimeSpan PruneAfterExpiry = TimeSpan.FromDays(7);

        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MagicLinkService> _logger;

        public MagicLinkService(AppDbContext context, IJwtAuthService jwtAuthService, IConfiguration configuration, ILogger<MagicLinkService> logger)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> CreateLinkAsync(Guid userId, Guid hospitalId, string targetPath, string purpose, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
            if (hospitalId == Guid.Empty) throw new ArgumentException("A hospital is required.", nameof(hospitalId));
            if (!IsSafeRelativePath(targetPath)) throw new ArgumentException("Target must be an in-app relative path.", nameof(targetPath));

            var now = DateTime.UtcNow;
            var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var row = new MagicLoginToken
            {
                TokenId = Guid.NewGuid(),
                TokenHash = HashToken(token),
                UserId = userId,
                HospitalId = hospitalId,
                TargetPath = targetPath,
                Purpose = purpose.Length <= 60 ? purpose : purpose[..60],
                CreatedAt = now,
                ExpiresAt = now.AddHours(ResolveLifetimeHours()),
            };
            _context.MagicLoginTokens.Add(row);

            // Keep the table small without needing a scheduled job: drop a bounded batch of links that
            // expired more than a week ago whenever a new one is issued.
            var pruneBefore = now - PruneAfterExpiry;
            var stale = await _context.MagicLoginTokens
                .Where(t => t.ExpiresAt < pruneBefore)
                .Take(PruneBatchSize)
                .ToListAsync(cancellationToken);
            if (stale.Count > 0) _context.MagicLoginTokens.RemoveRange(stale);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // e.g. the MagicLoginToken table isn't deployed yet. Un-track the rows so they don't
                // poison the caller's next SaveChanges on this same (scoped) context.
                _context.Entry(row).State = EntityState.Detached;
                foreach (var old in stale) _context.Entry(old).State = EntityState.Detached;
                throw;
            }

            // The token rides in the URL FRAGMENT, not the query string: fragments are never sent to a
            // server, so it stays out of web-server/proxy access logs and Referer headers, and WhatsApp's
            // link-preview crawler (which only makes a plain GET) never sees it.
            return $"{ResolveWebAppBaseUrl()}/magic-login#t={token}";
        }

        public async Task<MagicLinkExchangeResult> ExchangeAsync(string token, string? clientIp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 32 || token.Length > 128)
                return Fail("malformed token");

            var now = DateTime.UtcNow;
            var hash = HashToken(token);
            var link = await _context.MagicLoginTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

            if (link == null) return Fail("unknown token");
            if (link.ConsumedAt != null) return Fail("token already used", link);
            if (link.ExpiresAt <= now) return Fail("token expired", link);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == link.UserId, cancellationToken);
            if (user == null || user.UserStatusId != (int)UserStatusEnum.Active) return Fail("user missing or not active", link);

            var userAuth = await _context.UserAuths.FirstOrDefaultAsync(a => a.UserID == link.UserId, cancellationToken);
            if (userAuth == null || userAuth.IsLocked) return Fail("account missing or locked", link);

            // The user must still belong to the hospital the link was issued for (staff can be removed
            // between the message being sent and the link being tapped).
            var isMember = await _context.HospitalUsers
                .AnyAsync(h => h.UserID == link.UserId && h.HospitalID == link.HospitalId, cancellationToken);
            if (!isMember) return Fail("user no longer belongs to hospital", link);

            // Burn the token BEFORE issuing a session. RowVersion makes this an optimistic-concurrency
            // write: if two requests race with the same token, only one SaveChanges succeeds.
            link.ConsumedAt = now;
            link.ConsumedIp = Truncate(clientIp, 64);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("token redeemed concurrently", link);
            }

            var roles = await _context.UserRoles
                .Where(ur => ur.UserID == user.UserID)
                .Join(_context.Roles, ur => ur.RoleID, r => r.RoleID, (ur, r) => r.RoleName)
                .ToListAsync(cancellationToken);

            // Same claim set a password/OTP login issues, so every downstream authorization check
            // treats this session identically.
            var claims = new List<Claim>
            {
                new(ClaimTypes.Email, user.Email ?? ""),
                new(ClaimTypes.MobilePhone, user.MobileNumber ?? ""),
                new("userId", user.UserID.ToString()),
                new("roles", string.Join(",", roles)),
                new("isLoginWithOp", "False"),
            };
            var accessToken = _jwtAuthService.GenerateJwtToken(claims);

            userAuth.LastLoginTime = now;
            userAuth.LoginMethod = "MagicLink";
            userAuth.LastLoginIP = Truncate(clientIp, 64);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Magic-link sign-in: user {UserId}, hospital {HospitalId}, purpose {Purpose}", link.UserId, link.HospitalId, link.Purpose);

            return new MagicLinkExchangeResult
            {
                Success = true,
                Message = "Login Successful",
                AccessToken = accessToken,
                UserId = user.UserID,
                HospitalId = link.HospitalId,
                TargetPath = link.TargetPath,
            };
        }

        private MagicLinkExchangeResult Fail(string reason, MagicLoginToken? link = null)
        {
            _logger.LogWarning("Magic-link exchange rejected ({Reason}) tokenId={TokenId} userId={UserId}", reason, link?.TokenId, link?.UserId);
            return new MagicLinkExchangeResult { Success = false, Message = InvalidLinkMessage };
        }

        private int ResolveLifetimeHours()
        {
            var configured = int.TryParse(_configuration["MagicLink:LifetimeHours"], out var hours) ? hours : DefaultLifetimeHours;
            return Math.Clamp(configured, 1, MaxLifetimeHours);
        }

        private string ResolveWebAppBaseUrl() =>
            (_configuration["WebApp:BaseUrl"] ?? "https://1hms.nexeagle.com").TrimEnd('/');

        // A landing path must stay inside the app: no scheme, no protocol-relative "//host", no
        // backslash tricks. (Links are only ever created server-side, so this is defence in depth.)
        internal static bool IsSafeRelativePath(string? path) =>
            !string.IsNullOrEmpty(path)
            && path.Length <= MaxTargetPathLength
            && path[0] == '/'
            && !path.StartsWith("//", StringComparison.Ordinal)
            && !path.Contains('\\')
            && !path.Contains("://", StringComparison.Ordinal)
            && !path.Any(char.IsControl);

        internal static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string? Truncate(string? value, int max) =>
            value == null ? null : (value.Length <= max ? value : value[..max]);
    }
}
