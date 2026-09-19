using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    // Regression coverage for the free-tier monthly action limit -- this exact gate has already
    // been silently disabled once (accidentally left that way after a "disable it" request never
    // got reverted) and had to be restored from git history. These tests exist so a future
    // accidental "always return false" in IsGatedAsync fails CI instead of shipping unnoticed.
    [TestFixture]
    public class UsageLimitServiceTests
    {
        private AppDbContext _context = null!;
        private UsageLimitService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _service = new UsageLimitService(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task GetStatusAsync_HospitalWithNoSubscriptionRow_DefaultsToTrialAndIsGated()
        {
            // No HospitalSubscriptions row at all -- HospitalAccessFilter treats this the same as
            // "Trial", and so must the usage gate.
            var hospitalId = Guid.NewGuid();

            var result = await _service.GetStatusAsync(hospitalId, CancellationToken.None);

            Assert.That(result.Limit, Is.EqualTo(100), "A hospital with no subscription row must default to the 100-action Trial limit, not go unlimited.");
            Assert.That(result.Allowed, Is.True, "Zero usage so far must still be under the limit.");
        }

        [Test]
        public async Task GetStatusAsync_TrialHospital_IsGatedAtFallbackLimit()
        {
            var hospitalId = Guid.NewGuid();
            _context.HospitalSubscriptions.Add(new HospitalSubscription
            {
                HospitalSubscriptionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                Status = "Trial",
            });
            await _context.SaveChangesAsync();

            var result = await _service.GetStatusAsync(hospitalId, CancellationToken.None);

            Assert.That(result.Limit, Is.EqualTo(100), "Trial hospitals must be capped at the 100-action monthly limit.");
        }

        [Test]
        public async Task GetStatusAsync_ActiveHospital_IsNotGated()
        {
            // The one status this gate has always exempted -- a paying hospital gets unlimited
            // actions, same behavior the erroneous "disable it entirely" version gave to Trial too.
            var hospitalId = Guid.NewGuid();
            _context.HospitalSubscriptions.Add(new HospitalSubscription
            {
                HospitalSubscriptionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                Status = "Active",
            });
            await _context.SaveChangesAsync();

            var result = await _service.GetStatusAsync(hospitalId, CancellationToken.None);

            Assert.That(result.Limit, Is.EqualTo(int.MaxValue), "An Active (paid) subscription must be unlimited.");
            Assert.That(result.Allowed, Is.True);
        }

        [Test]
        public async Task GetStatusAsync_TrialHospitalOverFallbackLimit_ReportsNotAllowed()
        {
            var hospitalId = Guid.NewGuid();
            _context.HospitalSubscriptions.Add(new HospitalSubscription
            {
                HospitalSubscriptionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                Status = "Trial",
            });
            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            _context.HospitalMonthlyUsage.Add(new HospitalMonthlyUsage
            {
                HospitalId = hospitalId,
                YearMonth = yearMonth,
                UsedCount = 100,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var result = await _service.GetStatusAsync(hospitalId, CancellationToken.None);

            Assert.That(result.Allowed, Is.False, "100 actions already used against a 100 limit must block further actions.");
            Assert.That(result.Message, Does.Contain("Upgrade your plan"));
        }
    }
}
