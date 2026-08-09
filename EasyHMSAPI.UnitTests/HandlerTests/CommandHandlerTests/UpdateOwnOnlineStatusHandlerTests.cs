using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateOwnOnlineStatusHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateOwnOnlineStatusHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateOwnOnlineStatusHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_CallerHasDoctorProfile_FlipsOwnFlag()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            doctor.IsOnlineNow = false;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateOwnOnlineStatusRequestModel
            {
                CallerUserId = user.UserID,
                IsOnlineNow = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.Doctors.FirstAsync(d => d.DoctorID == doctor.DoctorID);
            Assert.That(updated.IsOnlineNow, Is.True);
        }

        [Test]
        public async Task Handle_CallerHasNoDoctorProfile_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateOwnOnlineStatusRequestModel
            {
                CallerUserId = user.UserID,
                IsOnlineNow = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
