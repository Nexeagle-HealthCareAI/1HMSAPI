using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Linq;
using EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DoctorOverrideCreateHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorOverrideCreateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorOverrideCreateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesOverride()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1),
                OverrideDate = DateTime.UtcNow,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00",
                        EndTime = "13:00",
                        SlotDurationInMinutes = 15
                    }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var overrides = await _context.DoctorShiftOverrides.Where(o => o.DoctorID == doctor.DoctorID).ToListAsync();
            Assert.That(overrides.Count, Is.EqualTo(1));
            Assert.That(overrides[0].ShiftName, Is.EqualTo("Morning"));
        }

        [Test]
        public async Task Handle_InvalidShiftName_ReturnsFailure()
        {
             // Arrange
            var request = new DoctorOverrideCreateRequestModel
            {
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails { ShiftName = "InvalidShift" }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Allowed values are"));
        }

        [Test]
        public async Task Handle_ExistingOverride_UpdatesOverride()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var date = DateTime.Today;

            var existingOverride = new DoctorShiftOverride
            {
                OverrideID = Guid.NewGuid(),
                DoctorID = doctor.DoctorID,
                HospitalId = hospitalId,
                ShiftName = "Morning",
                StartDate = date,
                EndDate = date,
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(12)
            };
            _context.DoctorShiftOverrides.Add(existingOverride);
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                StartDate = date,
                EndDate = date,
                OverrideDate = date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "10:00",
                        EndTime = "14:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Does.Contain("updated"));
            
            var updated = await _context.DoctorShiftOverrides.FindAsync(existingOverride.OverrideID);
            Assert.That(updated!.StartTime, Is.EqualTo(TimeSpan.FromHours(10)));
        }
    }
}
