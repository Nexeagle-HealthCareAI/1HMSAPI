using System;
using System.Security.Cryptography;
using System.Text;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // The abdm-m1 skill's encryption rule: take the padding from the certificate response's
    // encryptionAlgorithm, and refuse to encrypt when it names something unrecognised.
    [TestFixture]
    public class AbdmCertificateTests
    {
        private const string M1Algorithm = "RSA/ECB/OAEPWithSHA-1AndMGF1Padding";

        private static string Body(string publicKeyBase64, string? algorithm) =>
            algorithm == null
                ? $"{{\"publicKey\":\"{publicKeyBase64}\"}}"
                : $"{{\"publicKey\":\"{publicKeyBase64}\",\"encryptionAlgorithm\":\"{algorithm}\"}}";

        [TestCase(M1Algorithm)]
        [TestCase("rsa/ecb/oaepwithsha-1andmgf1padding")]
        [TestCase("  RSA/ECB/OAEPWithSHA-1AndMGF1Padding  ")]
        public void M1Algorithm_MapsToOaepSha1(string algorithm)
        {
            Assert.That(AbdmCertificate.PaddingFor(algorithm), Is.EqualTo(RSAEncryptionPadding.OaepSHA1));
        }

        [Test]
        public void M4Algorithm_MapsToPkcs1()
        {
            Assert.That(AbdmCertificate.PaddingFor("RSA/ECB/PKCS1Padding"), Is.EqualTo(RSAEncryptionPadding.Pkcs1));
        }

        [TestCase("RSA/ECB/OAEPWithSHA-256AndMGF1Padding")]
        [TestCase("AES/GCM/NoPadding")]
        [TestCase("")]
        [TestCase(null)]
        public void UnrecognisedAlgorithm_IsNotGuessed(string? algorithm)
        {
            Assert.That(AbdmCertificate.PaddingFor(algorithm), Is.Null);
        }

        [Test]
        public void Parse_UnknownAlgorithm_RefusesRatherThanDefaulting()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => AbdmCertificate.Parse(Body("AAAA", "RSA/ECB/OAEPWithSHA-256AndMGF1Padding")));
            Assert.That(ex!.Message, Does.Contain("OAEPWithSHA-256"));
        }

        [Test]
        public void Parse_MissingAlgorithm_Refuses()
        {
            Assert.Throws<InvalidOperationException>(() => AbdmCertificate.Parse(Body("AAAA", null)));
        }

        [Test]
        public void Parse_NonJsonBody_Refuses()
        {
            Assert.Throws<InvalidOperationException>(() => AbdmCertificate.Parse("-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----"));
        }

        [Test]
        public void Parse_BareBase64_GetsPemArmour()
        {
            var key = AbdmCertificate.Parse(Body("AAAA", M1Algorithm));
            Assert.That(key.Pem, Does.StartWith("-----BEGIN PUBLIC KEY-----"));
            Assert.That(key.Algorithm, Is.EqualTo(M1Algorithm));
        }

        [Test]
        public void RoundTrip_EncryptWithParsedKey_DecryptsWithPrivateKey()
        {
            using var keyPair = RSA.Create(2048);
            var publicKeyBase64 = Convert.ToBase64String(keyPair.ExportSubjectPublicKeyInfo());
            var parsed = AbdmCertificate.Parse(Body(publicKeyBase64, M1Algorithm));

            using var publicRsa = RSA.Create();
            publicRsa.ImportFromPem(parsed.Pem);
            var cipher = publicRsa.Encrypt(Encoding.UTF8.GetBytes("123456"), parsed.Padding);

            var plain = Encoding.UTF8.GetString(keyPair.Decrypt(cipher, RSAEncryptionPadding.OaepSHA1));
            Assert.That(plain, Is.EqualTo("123456"));
        }
    }
}
