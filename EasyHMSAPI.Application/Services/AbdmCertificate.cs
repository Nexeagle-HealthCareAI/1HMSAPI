using System.Security.Cryptography;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    public sealed record AbdmPublicKey(string Pem, string Algorithm, RSAEncryptionPadding Padding);

    /// <summary>Reads ABDM's certificate response, <c>{"publicKey", "encryptionAlgorithm"}</c>. The
    /// padding comes from the response, never from a constant: a hard-coded padding keeps working
    /// until ABDM changes it, and then fails looking like a bad OTP or Aadhaar number.</summary>
    public static class AbdmCertificate
    {
        /// <summary>The algorithm names ABDM documents. M1 and PHR use OAEP with SHA-1 for both
        /// digest and MGF1; the M4 registries use PKCS#1 v1.5. Anything else returns null and is
        /// refused — .NET's OaepSHA256, for one, is not what Java means by OAEPWithSHA-256.</summary>
        public static RSAEncryptionPadding? PaddingFor(string? algorithm) =>
            algorithm?.Trim().ToUpperInvariant() switch
            {
                "RSA/ECB/OAEPWITHSHA-1ANDMGF1PADDING" => RSAEncryptionPadding.OaepSHA1,
                "RSA/ECB/PKCS1PADDING" => RSAEncryptionPadding.Pkcs1,
                _ => null
            };

        public static AbdmPublicKey Parse(string body)
        {
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(body);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("ABDM's certificate response was not JSON, so it names no encryption algorithm; ABHA calls are paused.");
            }

            var key = ReadString(root, "publicKey") ?? ReadString(root, "certificate");
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("ABDM's certificate response did not contain a public key.");

            var algorithm = ReadString(root, "encryptionAlgorithm");
            var padding = PaddingFor(algorithm)
                ?? throw new InvalidOperationException(
                    $"ABDM's certificate names an encryption algorithm this system does not support (\"{algorithm ?? "none"}\"); ABHA calls are paused until it is updated.");

            var label = root.TryGetProperty("publicKey", out _) ? "PUBLIC KEY" : "CERTIFICATE";
            return new AbdmPublicKey(WrapAsPemIfBare(key, label), algorithm!.Trim(), padding);
        }

        /// <summary>The sandbox sends the key as bare base64 DER with no PEM armour, which
        /// RSA.ImportFromPem rejects, so armour is added when it is missing.</summary>
        private static string WrapAsPemIfBare(string value, string label) =>
            value.Contains("-----BEGIN", StringComparison.Ordinal)
                ? value
                : $"-----BEGIN {label}-----\n{value.Trim()}\n-----END {label}-----";

        private static string? ReadString(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
