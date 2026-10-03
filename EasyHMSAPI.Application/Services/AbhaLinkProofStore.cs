using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>The ABHA profile ABDM returned after a successful OTP login.</summary>
    public sealed record VerifiedAbhaProfile(string AbhaNumber, string? AbhaAddress, string? FullName, string? Gender, string? DateOfBirth, string? Mobile);

    /// <summary>
    /// Proof that an ABHA profile was really verified with ABDM by this staff member for this hospital. After a successful OTP login the API
    /// issues a one-time token; linking the ABHA to the hospital needs that token and takes the demographics from what ABDM returned, never
    /// from the client. Single use, short-lived, and bound to the user and hospital.
    /// </summary>
    public interface IAbhaLinkProofStore
    {
        string Issue(Guid userId, Guid hospitalId, VerifiedAbhaProfile profile);
        /// <summary>Returns the verified profile and invalidates the token, or null when the token is unknown, expired, used or belongs to someone else.</summary>
        VerifiedAbhaProfile? Consume(string? token, Guid userId, Guid hospitalId);
    }

    public sealed class AbhaLinkProofStore : IAbhaLinkProofStore
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
        private const string Prefix = "Abdm:LinkProof:";
        private readonly IMemoryCache _cache;

        public AbhaLinkProofStore(IMemoryCache cache) => _cache = cache;

        private sealed record Entry(Guid UserId, Guid HospitalId, VerifiedAbhaProfile Profile);

        public string Issue(Guid userId, Guid hospitalId, VerifiedAbhaProfile profile)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            _cache.Set(Prefix + token, new Entry(userId, hospitalId, profile), Lifetime);
            return token;
        }

        public VerifiedAbhaProfile? Consume(string? token, Guid userId, Guid hospitalId)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var key = Prefix + token;
            if (!_cache.TryGetValue(key, out Entry? entry) || entry == null) return null;
            if (entry.UserId != userId || entry.HospitalId != hospitalId) return null;   // leave it in place for its rightful owner
            _cache.Remove(key);
            return entry.Profile;
        }
    }
}
