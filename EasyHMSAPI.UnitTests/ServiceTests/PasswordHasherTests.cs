using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class PasswordHasherTests
    {
        private Mock<IMaskingService> _masking = null!;

        [SetUp]
        public void SetUp()
        {
            _masking = new Mock<IMaskingService>();
            _masking.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _masking.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => "masked:" + s);
        }

        [Test]
        public void Hash_is_salted_and_verifies_only_the_right_password()
        {
            var a = PasswordHasher.Hash("Secret#123", iterations: 1000);
            var b = PasswordHasher.Hash("Secret#123", iterations: 1000);

            Assert.That(a, Is.Not.EqualTo(b), "same password must produce different hashes (salt)");
            Assert.That(PasswordHasher.IsModern(a), Is.True);
            Assert.That(a.Length, Is.LessThan(256));
            Assert.That(PasswordHasher.Verify("Secret#123", a, _masking.Object), Is.True);
            Assert.That(PasswordHasher.Verify("secret#123", a, _masking.Object), Is.False);
            Assert.That(PasswordHasher.Verify("", a, _masking.Object), Is.False);
        }

        [Test]
        public void Legacy_sha256_hash_still_verifies_and_is_flagged_for_rehash()
        {
            var legacy = PasswordHasher.LegacyHash("Secret#123");

            Assert.That(PasswordHasher.IsModern(legacy), Is.False);
            Assert.That(PasswordHasher.NeedsRehash(legacy), Is.True);
            Assert.That(PasswordHasher.Verify("Secret#123", legacy, _masking.Object), Is.True);
            Assert.That(PasswordHasher.Verify("other", legacy, _masking.Object), Is.False);
        }

        [Test]
        public void Legacy_masked_hash_verifies_when_masking_is_enabled()
        {
            _masking.Setup(m => m.IsMaskingEnabled()).Returns(true);
            var stored = "masked:" + PasswordHasher.LegacyHash("Secret#123");

            Assert.That(PasswordHasher.Verify("Secret#123", stored, _masking.Object), Is.True);
        }

        [Test]
        public void Weaker_iteration_count_needs_rehash_but_current_does_not()
        {
            Assert.That(PasswordHasher.NeedsRehash(PasswordHasher.Hash("x", iterations: 1000)), Is.True);
            Assert.That(PasswordHasher.NeedsRehash(PasswordHasher.Hash("x")), Is.False);
            Assert.That(PasswordHasher.NeedsRehash(null), Is.False);
        }

        [Test]
        public void Malformed_modern_hash_never_verifies()
        {
            Assert.That(PasswordHasher.Verify("x", "PBKDF2-SHA256$abc$!!$??", _masking.Object), Is.False);
            Assert.That(PasswordHasher.Verify("x", "PBKDF2-SHA256$1000$AAAA", _masking.Object), Is.False);
        }
    }
}
