using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Services
{
    // Short, unique, QR-friendly hospital code generator -- same charset/length and collision-retry
    // shape as CMSAPI's ReferralCodeService generator, adapted for this DbContext.
    public static class HospitalCodeHelper
    {
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int Length = 6;

        public static async Task<string> GenerateUniqueCodeAsync(AppDbContext context, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = Generate();
                var exists = await context.Hospitals.AnyAsync(h => h.HospitalCode == candidate, cancellationToken);
                if (!exists) return candidate;
            }
            throw new InvalidOperationException("Could not generate a unique hospital code. Please try again.");
        }

        private static string Generate()
        {
            using var rng = RandomNumberGenerator.Create();
            var result = new char[Length];
            var buffer = new byte[Length];
            rng.GetBytes(buffer);
            for (var i = 0; i < Length; i++)
            {
                result[i] = Chars[buffer[i] % Chars.Length];
            }
            return new string(result);
        }
    }
}
