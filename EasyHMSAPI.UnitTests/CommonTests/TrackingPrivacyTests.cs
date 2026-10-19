using EasyHMSAPI.Application.Common;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.CommonTests
{
    [TestFixture]
    public class TrackingPrivacyTests
    {
        private const string Key = "unit-test-key-0123456789abcdef-0123456789";

        [TestCase("203.0.113.57", "203.0.113.0")]
        [TestCase("::ffff:203.0.113.57", "203.0.113.0")]
        [TestCase("2001:db8:85a3:8d3:1319:8a2e:370:7348", "2001:db8:85a3::")]
        [TestCase("not-an-ip", null)]
        [TestCase(null, null)]
        public void TruncateIp_KeepsOnlyTheNetworkPart(string? input, string? expected)
        {
            Assert.That(TrackingPrivacy.TruncateIp(input), Is.EqualTo(expected));
        }

        [TestCase("+91 98765 43210", "9876543210")]
        [TestCase("919876543210", "9876543210")]
        [TestCase("09876543210", "9876543210")]
        [TestCase("123", null)]
        public void NormalizeMobile_LastTenDigits(string input, string? expected)
        {
            Assert.That(TrackingPrivacy.NormalizeMobile(input), Is.EqualTo(expected));
        }

        [Test]
        public void MaskMobile_KeepsFirstAndLastTwo()
        {
            Assert.That(TrackingPrivacy.MaskMobile("9876543210"), Is.EqualTo("98XXXXXX10"));
        }

        [Test]
        public void HashMobile_IsStableKeyedAndNeedsAStrongKey()
        {
            var a = TrackingPrivacy.HashMobile("9876543210", Key);
            Assert.That(a, Has.Length.EqualTo(64));
            Assert.That(TrackingPrivacy.HashMobile("9876543210", Key), Is.EqualTo(a));
            Assert.That(TrackingPrivacy.HashMobile("9876543211", Key), Is.Not.EqualTo(a));
            Assert.That(TrackingPrivacy.HashMobile("9876543210", Key + "x"), Is.Not.EqualTo(a));
            Assert.That(TrackingPrivacy.HashMobile("9876543210", "short"), Is.Null);
            Assert.That(TrackingPrivacy.HashMobile("9876543210", null), Is.Null);
        }
    }
}
