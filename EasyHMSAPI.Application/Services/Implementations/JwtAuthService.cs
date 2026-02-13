using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class JwtAuthService : IJwtAuthService
    {
        private readonly string _jwtSecretKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public JwtAuthService(IConfiguration configuration)
        {
            _jwtSecretKey = configuration["Jwt:SecretKey"] ?? string.Empty;
            _jwtIssuer = configuration["Jwt:Issuer"] ?? string.Empty;
            _jwtAudience = configuration["Jwt:Audience"] ?? string.Empty;
        }

        public string GenerateJwtToken(List<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
