using System;
using System.Security.Cryptography;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 5: record encryption between HIP and HIU. The known answers below were worked
    // out independently of this code (plain modular arithmetic on curve25519's Weierstrass form,
    // standard HKDF-SHA256 and AES-256-GCM), so a pass means the scheme itself is right, not just
    // that Encrypt and Decrypt agree with each other. Interop with the Fidelius CLI is still to
    // be proved against the tool itself.
    [TestFixture]
    public class AbdmFideliusTests
    {
        private const string HipPrivate = "BybQG4d9NUZqogF/3lWUOfkF0Z045TDm5XKkbAxfq+A=";
        private const string HipPublic = "BAOpW2lN9Yuh9e9mrb2SY2P5Sc+UHuug4/fiGRTqfhqUNbO1bTCvS79vbQXfYxeyLu7ufk7Kl2X0OemW7oNSm/I=";
        private const string HiuPrivate = "D2aWzZJ9miOoVnZe0mLWpkrSzNdAUFzRDXtJ1pZr1Rs=";
        private const string HiuPublic = "BAy/BgpqAp4wA6W5NasOKyBnZPKAihkRjXjd6n5fKvzmekJcA12CsuGfsg+Fc1WxNPgUlI1qztS9q7T88RGbHPY=";
        private const string HipNonce = "6j6G4jaJ2vb4coDmCWRQBoslOXkyMu8gpmL2XV3wdt8=";
        private const string HiuNonce = "PKeK26YEzZ1O1nKqeCMpVtDmvNT/bbzLr5AZexZ6CiE=";
        private const string SharedX = "EAe1AazVKbayJvgRT2WO6W8mRXKiJ3ruVxaF2hiujHA=";
        private const string Plain = "{\"resourceType\":\"Bundle\",\"id\":\"known-answer\"}";
        private const string Cipher = "ji+4zRpeU2S3FVhfO3J8/uLlE2kc8KreGmeFk3anEM0IELFzTo6n4fOayMAHk2DlKXvuBXR6xi5MiA7irg==";

        [Test]
        public void PublicKey_IsThePointForThePrivateKey()
        {
            Assert.That(AbdmFidelius.PublicKeyFor(HipPrivate), Is.EqualTo(HipPublic));
            Assert.That(AbdmFidelius.PublicKeyFor(HiuPrivate), Is.EqualTo(HiuPublic));
        }

        [Test]
        public void SharedSecret_IsTheSameFromBothSides_AndMatchesTheKnownAnswer()
        {
            Assert.That(AbdmFidelius.SharedSecret(HipPrivate, HiuPublic), Is.EqualTo(SharedX));
            Assert.That(AbdmFidelius.SharedSecret(HiuPrivate, HipPublic), Is.EqualTo(SharedX));
        }

        [Test]
        public void Encrypt_GivesTheKnownCipherText()
        {
            Assert.That(AbdmFidelius.Encrypt(Plain, HipPrivate, HipNonce, HiuPublic, HiuNonce), Is.EqualTo(Cipher));
        }

        [Test]
        public void Decrypt_AsTheOtherSide_GivesThePlainTextBack()
        {
            Assert.That(AbdmFidelius.Decrypt(Cipher, HiuPrivate, HiuNonce, HipPublic, HipNonce), Is.EqualTo(Plain));
        }

        [Test]
        public void FreshKeyMaterial_RoundTrips_AndIsDifferentEveryTime()
        {
            var hip = AbdmFidelius.NewKeyMaterial();
            var hiu = AbdmFidelius.NewKeyMaterial();
            Assert.That(hip.PrivateKey, Is.Not.EqualTo(AbdmFidelius.NewKeyMaterial().PrivateKey));
            Assert.That(Convert.FromBase64String(hip.Nonce), Has.Length.EqualTo(32));
            Assert.That(Convert.FromBase64String(hip.PublicKey), Has.Length.EqualTo(65));

            var encrypted = AbdmFidelius.Encrypt("नमस्ते, record", hip.PrivateKey, hip.Nonce, hiu.PublicKey, hiu.Nonce);

            Assert.That(AbdmFidelius.Decrypt(encrypted, hiu.PrivateKey, hiu.Nonce, hip.PublicKey, hip.Nonce), Is.EqualTo("नमस्ते, record"));
        }

        [Test]
        public void TamperedContent_OrTheWrongNonce_FailsTheTagCheck()
        {
            var bytes = Convert.FromBase64String(Cipher);
            bytes[3] ^= 0x01;

            Assert.Throws<AuthenticationTagMismatchException>(() => AbdmFidelius.Decrypt(Convert.ToBase64String(bytes), HiuPrivate, HiuNonce, HipPublic, HipNonce));
            Assert.Catch<CryptographicException>(() => AbdmFidelius.Decrypt(Cipher, HiuPrivate, HipNonce, HipPublic, HipNonce));
        }

        [Test]
        public void APublicKeyThatIsNotOnTheCurve_IsRefused()
        {
            var bytes = Convert.FromBase64String(HiuPublic);
            bytes[40] ^= 0x01;

            Assert.Catch<CryptographicException>(() => AbdmFidelius.SharedSecret(HipPrivate, Convert.ToBase64String(bytes)));
            Assert.Catch<CryptographicException>(() => AbdmFidelius.SharedSecret(HipPrivate, Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        }
    }
}
