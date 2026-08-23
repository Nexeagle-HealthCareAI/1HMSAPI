using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class GenerateHospitalCodeHandlerTests
    {
        private AppDbContext _context = null!;
        private GenerateHospitalCodeHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GenerateHospitalCodeHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_NoExistingCode_GeneratesAndPersistsOne()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);

            var response = await _handler.Handle(new GenerateHospitalCodeRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HospitalCode, Is.Not.Null.And.Not.Empty);

            var reloaded = await _context.Hospitals.FindAsync(hospital.HospitalID);
            Assert.That(reloaded!.HospitalCode, Is.EqualTo(response.HospitalCode));
        }

        [Test]
        public async Task Handle_AlreadyHasCode_ReturnsExistingCode_DoesNotChangeIt()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            hospital.HospitalCode = "FIXED1";
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GenerateHospitalCodeRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HospitalCode, Is.EqualTo("FIXED1"));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GenerateHospitalCodeRequestModel { HospitalId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
