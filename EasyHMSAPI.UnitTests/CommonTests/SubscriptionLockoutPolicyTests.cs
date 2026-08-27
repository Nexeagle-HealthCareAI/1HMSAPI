using EasyHMSAPI.Api.Common;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.CommonTests
{
    [TestFixture]
    public class SubscriptionLockoutPolicyTests
    {
        // The bug this guards against: HospitalAccessFilter used to call subStatus.Equals(...)
        // directly on a string? sourced from an IMemoryCache read, which throws
        // NullReferenceException on a null status -- and since this runs as an action filter
        // (before any controller's own try/catch even starts), that exception surfaced as a raw,
        // unhandled 500 on every write request for the affected hospital, discharge-settings save
        // included.
        [Test]
        public void IsLockedOut_NullStatus_ReturnsFalse()
        {
            Assert.That(SubscriptionLockoutPolicy.IsLockedOut(null), Is.False);
        }

        [Test]
        public void IsRejected_NullStatus_ReturnsFalse()
        {
            Assert.That(SubscriptionLockoutPolicy.IsRejected(null), Is.False);
        }

        [TestCase("Expired")]
        [TestCase("Blocked")]
        [TestCase("Rejected")]
        [TestCase("expired")]
        [TestCase("BLOCKED")]
        public void IsLockedOut_KnownBadStatus_ReturnsTrue(string status)
        {
            Assert.That(SubscriptionLockoutPolicy.IsLockedOut(status), Is.True);
        }

        [TestCase("Active")]
        [TestCase("Trial")]
        [TestCase("")]
        [TestCase("SomeUnknownStatus")]
        public void IsLockedOut_ActiveOrUnknownStatus_ReturnsFalse(string status)
        {
            Assert.That(SubscriptionLockoutPolicy.IsLockedOut(status), Is.False);
        }

        [Test]
        public void IsRejected_OnlyTrueForRejected_CaseInsensitive()
        {
            Assert.That(SubscriptionLockoutPolicy.IsRejected("rejected"), Is.True);
            Assert.That(SubscriptionLockoutPolicy.IsRejected("Expired"), Is.False);
            Assert.That(SubscriptionLockoutPolicy.IsRejected("Blocked"), Is.False);
        }
    }
}
