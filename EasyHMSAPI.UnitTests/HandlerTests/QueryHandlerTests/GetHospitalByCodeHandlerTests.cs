using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetHospitalByCodeHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalByCodeHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalByCodeHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_KnownCode_ReturnsHospital()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            hospital.HospitalCode = "ABC123";
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalByCodeRequestModel { HospitalCode = "abc123" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HospitalId, Is.EqualTo(hospital.HospitalID));
            Assert.That(response.Name, Is.EqualTo(hospital.Name));
        }

        [Test]
        public async Task Handle_UnknownCode_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalByCodeRequestModel { HospitalCode = "NOPE99" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_InactiveHospital_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isActive: false);
            hospital.HospitalCode = "XYZ789";
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalByCodeRequestModel { HospitalCode = "XYZ789" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_BlankCode_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalByCodeRequestModel { HospitalCode = "" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
