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
            var isEnabled = _configuration["WhatsApp:IsEnabled"];
            return isEnabled?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
