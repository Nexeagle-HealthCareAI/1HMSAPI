using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using ECPoint = Org.BouncyCastle.Math.EC.ECPoint;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>One side's key material for a transfer: a fresh key pair and a 32-byte nonce.</summary>
    public sealed class AbdmKeyMaterial
    {
        /// <summary>Base64 of the 32-byte private scalar. Never leaves this server.</summary>
        public string PrivateKey { get; init; } = string.Empty;
        /// <summary>Base64 of the uncompressed point (0x04, X, Y): what the other side is sent.</summary>
        public string PublicKey { get; init; } = string.Empty;
        /// <summary>Base64 of 32 random bytes.</summary>
        public string Nonce { get; init; } = string.Empty;
    }

    /// <summary>
    /// ABDM's record encryption between HIP and HIU (abdm-m2 design, "How a health record is
    /// encrypted"), as the Fidelius reference implements it: ECDH on curve25519 in its Weierstrass
    /// form (BouncyCastle's "curve25519", not X25519), the two nonces XORed into a 20-byte HKDF salt
    /// and a 12-byte IV, HKDF-SHA256 over the shared point's x coordinate with empty info, then
    /// AES-256-GCM with no associated data and the 16-byte tag appended.
    /// The curve arithmetic is BouncyCastle's; HKDF and AES-GCM are .NET's own.
    /// </summary>
    public static class AbdmFidelius
    {
        public const string CryptoAlg = "ECDH";
        public const string Curve = "Curve25519";
        public const string Parameters = "Curve25519/32byte random key";
        private const int NonceBytes = 32;
        private const int SaltBytes = 20;
        private const int IvBytes = 12;
        private const int TagBytes = 16;

        private static readonly X9ECParameters Domain = CustomNamedCurves.GetByName("curve25519");

        /// <summary>A fresh key pair and nonce. One per transfer: that is what gives forward secrecy.</summary>
        public static AbdmKeyMaterial NewKeyMaterial()
        {
            var d = BigIntegers.CreateRandomInRange(BigInteger.One, Domain.N.Subtract(BigInteger.One), new SecureRandom());
            return new AbdmKeyMaterial
            {
                PrivateKey = Convert.ToBase64String(BigIntegers.AsUnsignedByteArray(32, d)),
                PublicKey = PublicKeyFor(d),
                Nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceBytes))
            };
        }

        /// <summary>The public key that belongs to a private key (base64 in, base64 out).</summary>
        public static string PublicKeyFor(string privateKey) =>
            PublicKeyFor(new BigInteger(1, Convert.FromBase64String(privateKey)));

        private static string PublicKeyFor(BigInteger d) =>
            Convert.ToBase64String(Domain.G.Multiply(d).Normalize().GetEncoded(false));

        /// <summary>Encrypts for the holder of <paramref name="theirPublicKey"/>. Returns base64 of
        /// ciphertext followed by the tag.</summary>
        public static string Encrypt(string plainText, string ourPrivateKey, string ourNonce, string theirPublicKey, string theirNonce)
        {
            var (key, iv) = SessionKey(ourPrivateKey, ourNonce, theirPublicKey, theirNonce);
            var plain = System.Text.Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagBytes];
            using var aes = new AesGcm(key, TagBytes);
            aes.Encrypt(iv, plain, cipher, tag);
            var output = new byte[cipher.Length + TagBytes];
            Buffer.BlockCopy(cipher, 0, output, 0, cipher.Length);
            Buffer.BlockCopy(tag, 0, output, cipher.Length, TagBytes);
            return Convert.ToBase64String(output);
        }

        /// <summary>Decrypts what the holder of <paramref name="theirPublicKey"/> encrypted for us.
        /// Throws <see cref="CryptographicException"/> when the tag does not verify.</summary>
        public static string Decrypt(string encrypted, string ourPrivateKey, string ourNonce, string theirPublicKey, string theirNonce)
        {
            var (key, iv) = SessionKey(ourPrivateKey, ourNonce, theirPublicKey, theirNonce);
            var input = Convert.FromBase64String(encrypted);
            if (input.Length < TagBytes)
                throw new CryptographicException("The encrypted content is shorter than its authentication tag.");
            var cipher = input.AsSpan(0, input.Length - TagBytes);
            var tag = input.AsSpan(input.Length - TagBytes);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(iv, cipher, tag, plain);
            return System.Text.Encoding.UTF8.GetString(plain);
        }

        /// <summary>The x coordinate of (our private key × their public key), 32 bytes, base64.
        /// Exposed so a known-answer test can pin the curve arithmetic.</summary>
        public static string SharedSecret(string ourPrivateKey, string theirPublicKey) =>
            Convert.ToBase64String(SharedX(ourPrivateKey, theirPublicKey));

        private static (byte[] Key, byte[] Iv) SessionKey(string ourPrivateKey, string ourNonce, string theirPublicKey, string theirNonce)
        {
            var ours = Convert.FromBase64String(ourNonce);
            var theirs = Convert.FromBase64String(theirNonce);
            if (ours.Length != NonceBytes || theirs.Length != NonceBytes)
                throw new CryptographicException("Both nonces must be 32 bytes.");
            var xor = new byte[NonceBytes];
            for (var i = 0; i < NonceBytes; i++)
                xor[i] = (byte)(ours[i] ^ theirs[i]);
            var salt = xor.AsSpan(0, SaltBytes).ToArray();
            var iv = xor.AsSpan(SaltBytes, IvBytes).ToArray();
            var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, SharedX(ourPrivateKey, theirPublicKey), 32, salt, info: null);
            return (key, iv);
        }

        private static byte[] SharedX(string ourPrivateKey, string theirPublicKey)
        {
            var d = new BigInteger(1, Convert.FromBase64String(ourPrivateKey));
            var shared = DecodePublicKey(theirPublicKey).Multiply(d).Normalize();
            if (shared.IsInfinity)
                throw new CryptographicException("The public key given is not usable for key agreement.");
            return shared.AffineXCoord.GetEncoded();
        }

        /// <summary>Peers send the public key either as the raw point or wrapped as X.509
        /// SubjectPublicKeyInfo; both are accepted. A point not on the curve is refused.</summary>
        private static ECPoint DecodePublicKey(string publicKey)
        {
            var bytes = Convert.FromBase64String(publicKey);
            try
            {
                if ((bytes.Length == 65 && bytes[0] == 0x04) || (bytes.Length == 33 && (bytes[0] == 0x02 || bytes[0] == 0x03)))
                    return Domain.Curve.DecodePoint(bytes);
                var parsed = PublicKeyFactory.CreateKey(bytes) as ECPublicKeyParameters
                    ?? throw new CryptographicException("The public key is not an elliptic curve key.");
                return Domain.Curve.DecodePoint(parsed.Q.GetEncoded(false));
            }
            catch (Exception ex) when (ex is not CryptographicException)
            {
                throw new CryptographicException("The public key could not be read as a curve25519 point.", ex);
            }
        }
    }
}
