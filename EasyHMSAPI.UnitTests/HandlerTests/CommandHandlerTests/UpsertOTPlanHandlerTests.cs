using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpsertOTPlanHandlerTests
    {
        private AppDbContext _context = null!;
        private UpsertOTPlanHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpsertOTPlanHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesPlan()
        {
            var hospitalId = Guid.NewGuid();
            var request = new UpsertOTPlanRequestModel
            {
                HospitalId = hospitalId,
                PlanName = "PCNL Plan",
                ProcedureName = "Percutaneous Nephrolithotomy",
                DefaultRoomCategory = "SEMI_PRIVATE",
                SuggestedIcuLevel = "LEVEL_2",
                IsActive = true,
                LoggedInUserName = "Dr Test",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.OtPlanId, Is.Not.Null);

            var saved = await _context.OTPlans.FindAsync(response.OtPlanId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.PlanName, Is.EqualTo("PCNL Plan"));
            Assert.That(saved.HospitalId, Is.EqualTo(hospitalId));
        }

        [Test]
        public async Task Handle_MissingPlanName_ReturnsError()
        {
            var request = new UpsertOTPlanRequestModel
            {
                HospitalId = Guid.NewGuid(),
                ProcedureName = "Some Procedure",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_ExistingPlanId_UpdatesInPlace()
        {
            var hospitalId = Guid.NewGuid();
            var existing = new OTPlan
            {
                OtPlanId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PlanName = "Old Name",
                ProcedureName = "Old Procedure",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.OTPlans.Add(existing);
            await _context.SaveChangesAsync();

            var request = new UpsertOTPlanRequestModel
            {
                OtPlanId = existing.OtPlanId,
                HospitalId = hospitalId,
                PlanName = "New Name",
                ProcedureName = "New Procedure",
                IsActive = false,
                LoggedInUserName = "Dr Test",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.OTPlans.FindAsync(existing.OtPlanId);
            Assert.That(updated!.PlanName, Is.EqualTo("New Name"));
            Assert.That(updated.IsActive, Is.False);
        }

        [Test]
        public async Task Handle_PackageTypeIds_RoundTrip_OnCreateAndUpdate()
        {
            var hospitalId = Guid.NewGuid();
            var fullPackage = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Full Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var nonPackage = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Non Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.PackageTypes.AddRange(fullPackage, nonPackage);
            await _context.SaveChangesAsync();

            var createRequest = new UpsertOTPlanRequestModel
            {
                HospitalId = hospitalId,
                PackageTypeIds = new List<Guid> { fullPackage.PackageTypeId, nonPackage.PackageTypeId },
                PlanName = "PCNL Plan",
                ProcedureName = "Percutaneous Nephrolithotomy",
            };

            var createResponse = await _handler.Handle(createRequest, CancellationToken.None);

            Assert.That(createResponse.Success, Is.True);
            var linksAfterCreate = _context.OTPlanPackageTypes.Where(l => l.OtPlanId == createResponse.OtPlanId).ToList();
            Assert.That(linksAfterCreate.Select(l => l.PackageTypeId), Is.EquivalentTo(new[] { fullPackage.PackageTypeId, nonPackage.PackageTypeId }));

            var thirdPackage = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "ICU Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.PackageTypes.Add(thirdPackage);
            await _context.SaveChangesAsync();

            var updateRequest = new UpsertOTPlanRequestModel
            {
                OtPlanId = createResponse.OtPlanId,
                HospitalId = hospitalId,
                PackageTypeIds = new List<Guid> { thirdPackage.PackageTypeId },
                PlanName = "PCNL Plan",
                ProcedureName = "Percutaneous Nephrolithotomy",
            };

            var updateResponse = await _handler.Handle(updateRequest, CancellationToken.None);

            Assert.That(updateResponse.Success, Is.True);
            var linksAfterUpdate = _context.OTPlanPackageTypes.Where(l => l.OtPlanId == createResponse.OtPlanId).ToList();
            Assert.That(linksAfterUpdate.Select(l => l.PackageTypeId), Is.EquivalentTo(new[] { thirdPackage.PackageTypeId }));

            var clearRequest = new UpsertOTPlanRequestModel
            {
                OtPlanId = createResponse.OtPlanId,
                HospitalId = hospitalId,
                PackageTypeIds = new List<Guid>(),
                PlanName = "PCNL Plan",
                ProcedureName = "Percutaneous Nephrolithotomy",
            };

            var clearResponse = await _handler.Handle(clearRequest, CancellationToken.None);

            Assert.That(clearResponse.Success, Is.True);
            Assert.That(_context.OTPlanPackageTypes.Any(l => l.OtPlanId == createResponse.OtPlanId), Is.False);
        }

        [Test]
        public async Task Handle_PlanIdNotFound_ReturnsError()
        {
            var request = new UpsertOTPlanRequestModel
            {
                OtPlanId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PlanName = "PCNL Plan",
                ProcedureName = "Procedure",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
