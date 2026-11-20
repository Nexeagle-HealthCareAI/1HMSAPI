using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>ABHA (M1) calls in the activity log: found missing on the sandbox on 2026-11-18,
    /// when a Create ABHA run left nothing to quote a REQUEST-ID from.</summary>
    [TestFixture]
    public class AbdmM1CallLogTests
    {
        private sealed class WithHospital : IRequest<string> { public Guid HospitalId { get; set; } }
        private sealed class WithOptionalHospital : IRequest<string> { public Guid? HospitalId { get; set; } }
        private sealed class WithoutHospital : IRequest<string> { public string TxnId { get; set; } = "t"; }

        [Test]
        public async Task TheHospitalOfARequest_IsNoted_AndARequestWithoutOneChangesNothing()
        {
            var scope = new AbdmCallScope();
            var hospital = Guid.NewGuid();

            Assert.That(await new AbdmHospitalScopeBehavior<WithHospital, string>(scope).Handle(new WithHospital { HospitalId = hospital }, _ => Task.FromResult("ok"), CancellationToken.None), Is.EqualTo("ok"));
            Assert.That(scope.HospitalId, Is.EqualTo(hospital));

            await new AbdmHospitalScopeBehavior<WithoutHospital, string>(scope).Handle(new WithoutHospital(), _ => Task.FromResult("ok"), CancellationToken.None);
            await new AbdmHospitalScopeBehavior<WithOptionalHospital, string>(scope).Handle(new WithOptionalHospital(), _ => Task.FromResult("ok"), CancellationToken.None);
            Assert.That(scope.HospitalId, Is.EqualTo(hospital));

            var other = Guid.NewGuid();
            await new AbdmHospitalScopeBehavior<WithOptionalHospital, string>(scope).Handle(new WithOptionalHospital { HospitalId = other }, _ => Task.FromResult("ok"), CancellationToken.None);
            Assert.That(scope.HospitalId, Is.EqualTo(other));
        }

        [TestCase("/abha/api/v3/enrollment/request/otp", "ABHA create: send OTP")]
        [TestCase("/abha/api/v3/enrollment/enrol/byAadhaar", "ABHA create: verify Aadhaar OTP")]
        [TestCase("/abha/api/v3/enrollment/auth/byAbdm", "ABHA create: verify mobile OTP")]
        [TestCase("/abha/api/v3/profile/login/verify", "ABHA verify: check OTP")]
        [TestCase("/abha/api/v3/profile/account/abha-card", "ABHA card")]
        [TestCase("/abha/api/v3/profile/account", "ABHA profile")]
        [TestCase("/abha/api/v3/something/new", "ABHA /abha/api/v3/something/new")]
        public void EachCall_HasAPlainName_ThatFitsTheColumn(string path, string name)
        {
            Assert.That(AbdmM1Calls.Name(path), Is.EqualTo(name));
            Assert.That(AbdmM1Calls.Name(new string('x', 400)).Length, Is.LessThanOrEqualTo(60));
        }

        [Test]
        public async Task ACall_IsWrittenWithItsRequestIdAndOutcome_AndARefusalKeepsAbdmsWords()
        {
            var database = "m1-log-" + Guid.NewGuid();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database));
            using var provider = services.BuildServiceProvider();
            var log = new AbdmM1CallLog(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AbdmM1CallLog>.Instance);
            var hospital = Guid.NewGuid();

            await log.RecordAsync(hospital, "req-1", "/abha/api/v3/enrollment/request/otp", 200, null, null, DateTime.UtcNow, CancellationToken.None);
            await log.RecordAsync(hospital, "req-2", "/abha/api/v3/enrollment/auth/byAbdm", 400, "ABDM-1100", "Invalid Scope", DateTime.UtcNow, CancellationToken.None);
            await log.RecordAsync(null, null, null, null, null, "no answer", DateTime.UtcNow, CancellationToken.None);

            using var scope = provider.CreateScope();
            var rows = scope.ServiceProvider.GetRequiredService<AppDbContext>().AbdmExchanges.ToList();
            var sent = rows.Single(r => r.RequestId == "req-1");
            Assert.That(sent.Status, Is.EqualTo(AbdmExchangeStatus.Done));
            Assert.That(sent.HospitalId, Is.EqualTo(hospital));
            Assert.That(sent.Kind, Is.EqualTo("ABHA create: send OTP"));
            Assert.That(sent.ExpectsCallback, Is.False);

            var refused = rows.Single(r => r.RequestId == "req-2");
            Assert.That(refused.Status, Is.EqualTo(AbdmExchangeStatus.Refused));
            Assert.That(refused.HttpStatus, Is.EqualTo(400));
            Assert.That(refused.ErrorCode, Is.EqualTo("ABDM-1100"));
            Assert.That(refused.ErrorMessage, Is.EqualTo("Invalid Scope"));

            Assert.That(rows.Count(r => r.RequestId.StartsWith("local-")), Is.EqualTo(1), "a call with no REQUEST-ID is still written");
        }
    }
}
