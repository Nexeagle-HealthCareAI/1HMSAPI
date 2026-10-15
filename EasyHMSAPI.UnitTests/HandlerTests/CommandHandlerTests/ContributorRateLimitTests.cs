using System.Linq;
using System.Reflection;
using EasyHMSAPI.Api.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// The strict per-address ceiling (8 a minute) is for the calls anyone can make without a session. A contributor page makes several calls at once
    /// (who am I, my articles, ...) and the Doctor Dekho server may be the address the API sees, so those calls must not share that ceiling.
    /// </summary>
    [TestFixture]
    public class ContributorRateLimitTests
    {
        private static string? PolicyOf(MemberInfo m) => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

        [Test]
        public void TheControllerAsAWhole_UsesTheNormalCeiling()
        {
            Assert.That(PolicyOf(typeof(ContributorController)), Is.EqualTo("PerIpPolicy"));
        }

        [TestCase("Invite")]
        [TestCase("SendOtp")]
        [TestCase("VerifyOtp")]
        [TestCase("SsoExchange")]
        [TestCase("SessionFromPatient")]
        public void TheCallsAnyoneCanMakeWithoutASession_UseTheStrictCeiling(string action)
        {
            var method = typeof(ContributorController).GetMethod(action)!;
            Assert.That(PolicyOf(method), Is.EqualTo("ContributorAuthPolicy"));
        }

        [Test]
        public void EveryOtherCall_HasNoStrictCeilingOfItsOwn()
        {
            var strict = new[] { "Invite", "SendOtp", "VerifyOtp", "SsoExchange", "SessionFromPatient" };
            var others = typeof(ContributorController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !strict.Contains(m.Name) && m.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Http")));

            Assert.That(others, Is.Not.Empty);
            foreach (var m in others) Assert.That(PolicyOf(m), Is.Null, m.Name);
        }
    }
}
