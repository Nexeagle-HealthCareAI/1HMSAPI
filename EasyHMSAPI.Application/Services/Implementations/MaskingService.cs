using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Services.Implementations
{
    [ExcludeFromCodeCoverage]
    public class MaskingService : IMaskingService
    {
        private readonly IConfiguration _configuration;

        public MaskingService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string Mask(string plaintext)
        {
            if (!IsMaskingEnabled())
            {
                return plaintext;
            }

            if (string.IsNullOrEmpty(plaintext))
            {
                return plaintext;
            }

            try
            {
                var pepper = _configuration["Security:OtpPepper"] ?? string.Empty;
                if (string.IsNullOrEmpty(pepper))
                {
                    return plaintext;
                }

                var key = Encoding.UTF8.GetBytes(pepper);
                var data = Encoding.UTF8.GetBytes(plaintext);
                var hash = HMACSHA256.HashData(key, data);
                return Convert.ToBase64String(hash);
            }
            catch
            {
                return plaintext;
            }
        }

        public string Unmask(string maskedValue)
        {
            if (!IsMaskingEnabled())
            {
                return maskedValue;
            }

            return maskedValue;
        }

        public bool IsMaskingEnabled()
        {
            // Credential hashing (OTPs + passwords) is controlled by a dedicated flag and must NOT
            // depend on whether WhatsApp messaging is enabled. For backwards-compatibility with
            // deployments that predate this flag, fall back to the legacy WhatsApp:IsEnabled value
            // (which previously, incorrectly, gated hashing). New deployments should set
            // Security:CredentialHashingEnabled explicitly. Note: hashing is only effective when a
            // non-empty Security:OtpPepper is configured (see Mask).
            var flag = _configuration["Security:CredentialHashingEnabled"];
            if (!string.IsNullOrWhiteSpace(flag))
            {
                return flag.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            var legacy = _configuration["WhatsApp:IsEnabled"];
            return legacy?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
