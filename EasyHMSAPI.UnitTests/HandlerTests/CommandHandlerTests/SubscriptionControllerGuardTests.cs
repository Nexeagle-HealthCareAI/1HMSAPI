using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Api.Controllers.V1;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Regression tests for subscription tenant isolation: status / payment-history / usage were readable
    /// for any hospital id by any signed-in user, and select-plan / submit-payment accepted any holder of
    /// an Admin role from any hospital.
    /// </summary>
    [TestFixture]
    public class SubscriptionControllerGuardTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalA;
        private Guid _hospitalB;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalA = Guid.NewGuid();
            _hospitalB = Guid.NewGuid();
            foreach (var h in new[] { _hospitalA, _hospitalB })
            {
                _context.HospitalSubscriptions.Add(new HospitalSubscription
                {
                    HospitalSubscriptionId = Guid.NewGuid(),
                    HospitalId = h,
                    Status = "Active",
                    PlanId = Guid.NewGuid(),
                    SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
                    PaymentReference = "SECRET-REF",
                    CreatedAt = DateTime.UtcNow,
                });
            }
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private SubscriptionController ControllerFor(Guid? userId)
        {
            var usage = new Mock<ISubscriptionLimitHelper>();
            usage.Setup(u => u.GetUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionUsage(null, 0, null, 0));
            var free = new Mock<IUsageLimitService>();
            free.Setup(f => f.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UsageLimitResult { Allowed = true, Limit = int.MaxValue });

            var controller = new SubscriptionController(_context, Mock.Of<IHttpClientFactory>(), Mock.Of<IConfiguration>(),
                Mock.Of<ILogger<SubscriptionController>>(), usage.Object, free.Object);

            var http = new DefaultHttpContext();
            if (userId.HasValue)
                http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", userId.Value.ToString()) }, "test"));
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return controller;
        }

        private Guid SeedMember(Guid hospitalId, string? adminRole = null)
        {
            var userId = Guid.NewGuid();
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = userId });
            if (adminRole != null)
            {
                var roleId = Guid.NewGuid();
                _context.Roles.Add(new Role { RoleID = roleId, HospitalID = hospitalId, RoleName = adminRole });
                _context.UserRoles.Add(new UserRole { UserID = userId, RoleID = roleId });
            }
            _context.SaveChanges();
            return userId;
        }

        private static int StatusOf(IActionResult r) => r is ObjectResult o ? (o.StatusCode ?? 200) : (r as StatusCodeResult)?.StatusCode ?? 0;

        [Test]
        public async Task Reads_MemberAllowed_StrangerAndAnonymousDenied()
        {
            var memberOfA = SeedMember(_hospitalA);

            Assert.That(StatusOf(await ControllerFor(memberOfA).GetSubscriptionStatus(_hospitalA)), Is.EqualTo(200));
            Assert.That(StatusOf(await ControllerFor(memberOfA).GetPaymentHistory(_hospitalA)), Is.EqualTo(200));
            Assert.That(StatusOf(await ControllerFor(memberOfA).GetUsage(_hospitalA)), Is.EqualTo(200));

            // Another hospital's data: status (with payment reference), history and usage.
            Assert.That(StatusOf(await ControllerFor(memberOfA).GetSubscriptionStatus(_hospitalB)), Is.EqualTo(403));
            Assert.That(StatusOf(await ControllerFor(memberOfA).GetPaymentHistory(_hospitalB)), Is.EqualTo(403));
            Assert.That(StatusOf(await ControllerFor(memberOfA).GetUsage(_hospitalB)), Is.EqualTo(403));

            Assert.That(StatusOf(await ControllerFor(null).GetSubscriptionStatus(_hospitalA)), Is.EqualTo(401));
        }

        [Test]
        public async Task Status_ExpiredHospitalStillReadableByItsOwnMember()
        {
            var sub = await _context.HospitalSubscriptions.FirstAsync(s => s.HospitalId == _hospitalA);
            sub.SubscriptionEndDate = DateTime.UtcNow.AddDays(-1);
            await _context.SaveChangesAsync();
            var member = SeedMember(_hospitalA);

            Assert.That(StatusOf(await ControllerFor(member).GetSubscriptionStatus(_hospitalA)), Is.EqualTo(200),
                "a locked-out hospital must still see its status so it can renew");
        }

        [Test]
        public async Task SelectPlan_AdminOfAnotherHospitalCannotChangeThisHospital()
        {
            var adminOfA = SeedMember(_hospitalA, "Admin");
            var before = (await _context.HospitalSubscriptions.AsNoTracking().FirstAsync(s => s.HospitalId == _hospitalB));

            var result = await ControllerFor(adminOfA).SelectPlan(_hospitalB, new SelectPlanRequest { PlanId = Guid.NewGuid() });

            Assert.That(StatusOf(result), Is.EqualTo(403));
            var after = await _context.HospitalSubscriptions.AsNoTracking().FirstAsync(s => s.HospitalId == _hospitalB);
            Assert.That(after.Status, Is.EqualTo("Active"));
            Assert.That(after.PlanId, Is.EqualTo(before.PlanId));
        }

        [Test]
        public async Task SelectPlan_AdminOfAStaffAtBIsNotAdminAtB()
        {
            // Same person: Admin at A, plain member at B.
            var admin = SeedMember(_hospitalA, "Admin");
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = _hospitalB, UserID = admin });
            _context.SaveChanges();

            var result = await ControllerFor(admin).SelectPlan(_hospitalB, new SelectPlanRequest { PlanId = Guid.NewGuid() });

            Assert.That(StatusOf(result), Is.EqualTo(403));
        }

        [Test]
        public async Task SelectPlan_OwnAdminAllowed()
        {
            var admin = SeedMember(_hospitalA, "AdminDoctor");
            var plan = Guid.NewGuid();

            var result = await ControllerFor(admin).SelectPlan(_hospitalA, new SelectPlanRequest { PlanId = plan });

            Assert.That(StatusOf(result), Is.EqualTo(200));
            var sub = await _context.HospitalSubscriptions.AsNoTracking().FirstAsync(s => s.HospitalId == _hospitalA);
            Assert.That(sub.PlanId, Is.EqualTo(plan));
        }

        [Test]
        public async Task SubmitPayment_ValidationAndAuthorisation()
        {
            var admin = SeedMember(_hospitalA, "Admin");
            var plainMember = SeedMember(_hospitalA);

            var forbidden = await ControllerFor(plainMember).SubmitPayment(_hospitalA, new SubmitPaymentRequest { Amount = 100, Reference = "UTR1" });
            var zero = await ControllerFor(admin).SubmitPayment(_hospitalA, new SubmitPaymentRequest { Amount = 0, Reference = "UTR1" });
            var blank = await ControllerFor(admin).SubmitPayment(_hospitalA, new SubmitPaymentRequest { Amount = 100, Reference = "   " });
            var ok = await ControllerFor(admin).SubmitPayment(_hospitalA, new SubmitPaymentRequest { Amount = 100, Reference = "  UTR1  " });

            Assert.That(StatusOf(forbidden), Is.EqualTo(403));
            Assert.That(StatusOf(zero), Is.EqualTo(400));
            Assert.That(StatusOf(blank), Is.EqualTo(400));
            Assert.That(StatusOf(ok), Is.EqualTo(200));
            var sub = await _context.HospitalSubscriptions.AsNoTracking().FirstAsync(s => s.HospitalId == _hospitalA);
            Assert.That(sub.PaymentReference, Is.EqualTo("UTR1"));
            Assert.That(sub.Status, Is.EqualTo("PendingApproval"));
        }
    }
}
