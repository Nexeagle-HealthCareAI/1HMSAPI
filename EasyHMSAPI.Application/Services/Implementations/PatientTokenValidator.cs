using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class PatientTokenValidator : IPatientTokenValidator
    {
        private readonly AppDbContext _context;
        private readonly TokenValidationParameters _validationParameters;

        public PatientTokenValidator(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? string.Empty,
                ValidAudience = configuration["Jwt:Audience"] ?? string.Empty,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"] ?? string.Empty)),
                ClockSkew = TimeSpan.FromMinutes(2),
            };
        }

        public async Task<PatientTokenValidationResult> ValidateAsync(string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return new PatientTokenValidationResult(false, null, "Missing bearer token.");
            }

            var token = authorizationHeader["Bearer ".Length..].Trim();
            ClaimsPrincipal principal;
            try
            {
                principal = new JwtSecurityTokenHandler().ValidateToken(token, _validationParameters, out _);
            }
            catch
            {
                return new PatientTokenValidationResult(false, null, "Invalid or expired session.");
            }

            // Same signing key as staff tokens (see JwtAuthService) — this claim is what actually
            // keeps the two identity spaces apart. Never accept a token missing it.
            var scope = principal.FindFirst("scope")?.Value;
            if (scope != "patient_public")
            {
                return new PatientTokenValidationResult(false, null, "Invalid session.");
            }

            var mobile = principal.FindFirst(ClaimTypes.MobilePhone)?.Value;
            var epochClaim = principal.FindFirst("sessionEpoch")?.Value;
            if (string.IsNullOrEmpty(mobile) || !int.TryParse(epochClaim, out var tokenEpoch))
            {
                return new PatientTokenValidationResult(false, null, "Invalid session.");
            }

            // Cross-check against the current epoch — a logout bumps this, which invalidates every
            // token issued before it even though they're still cryptographically unexpired.
            var currentEpoch = await _context.PublicPatientAuths
                .Where(a => a.Mobile == mobile)
                .Select(a => (int?)a.SessionEpoch)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentEpoch == null || currentEpoch.Value != tokenEpoch)
            {
                return new PatientTokenValidationResult(false, null, "Session has been signed out. Please log in again.");
            }

            return new PatientTokenValidationResult(true, mobile, null);
        }
    }
}
