using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DoctorTimeOffCreateHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorTimeOffCreateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorTimeOffCreateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesTimeOff()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();

            var request = new DoctorTimeOffCreateRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddDays(1),
                Reason = "Vacation"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var timeOff = await _context.DoctorTimeOffs.FirstOrDefaultAsync(t => t.DoctorID == doctor.DoctorID);
            Assert.That(timeOff, Is.Not.Null);
            Assert.That(timeOff.Reason, Is.EqualTo("Vacation"));
        }

        [Test]
        public async Task Handle_InvalidDateRange_ReturnsFailure()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            
            var request = new DoctorTimeOffCreateRequestModel
            {
                DoctorId = doctor.DoctorID,
                FromDate = DateTime.Today.AddDays(1),
                ToDate = DateTime.Today // To before From
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("toDate must be on or after fromDate"));
        }
    }
}
