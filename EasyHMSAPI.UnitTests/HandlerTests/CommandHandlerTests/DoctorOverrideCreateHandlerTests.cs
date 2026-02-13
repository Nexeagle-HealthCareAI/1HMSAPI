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
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

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
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_InvalidShiftName_ReturnsError()
        {
            // Arrange
            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails { ShiftName = "InvalidShift" }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Allowed values are Morning, Afternoon, Evening"));
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            // Arrange
            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                 ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails { ShiftName = "Morning" }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid Doctor Id"));
        }

        [Test]
        public async Task Handle_NewOverride_ReturnsSuccess()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddDays(7),
                OverrideDate = DateTime.UtcNow.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00",
                        EndTime = "12:00",
                        SlotDurationInMinutes = 30,
                        RecurringDays = new List<string> { "Monday" }
                    }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Doctor Override(s) added: 1"));

            var overrideRecord = await _context.DoctorShiftOverrides.FirstOrDefaultAsync(x => x.DoctorID == doctorId);
            Assert.That(overrideRecord, Is.Not.Null);
            Assert.That(overrideRecord!.ShiftName, Is.EqualTo("Morning"));
        }

        [Test]
        public async Task Handle_UpdateExistingOverride_ReturnsSuccess()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var overrideId = Guid.NewGuid();
            var startDate = DateTime.UtcNow.Date;
            var endDate = DateTime.UtcNow.Date.AddDays(7);

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            
            var existingOverride = TestEntityFactory.CreateDoctorShiftOverride(overrideId, doctorId, hospitalId);
            existingOverride.StartDate = startDate;
            existingOverride.EndDate = endDate;
            existingOverride.ShiftName = "Morning";
            _context.DoctorShiftOverrides.Add(existingOverride);
            
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate,
                OverrideDate = DateTime.UtcNow.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "10:00",
                        EndTime = "13:00",
                        SlotDurationInMinutes = 45,
                        RecurringDays = new List<string> { "Tuesday" }
                    }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Doctor Override(s) updated: 1, added: 0"));

            var overrideRecord = await _context.DoctorShiftOverrides.FirstOrDefaultAsync(x => x.OverrideID == overrideId);
            Assert.That(overrideRecord!.StartTime, Is.EqualTo(new TimeSpan(10, 0, 0)));
            Assert.That(overrideRecord.SlotDurationInMinutes, Is.EqualTo(45));
        }

        [Test]
        public async Task Handle_MultipleOverrides_AddAndUpdate_ReturnsSuccess()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var overrideId = Guid.NewGuid();
            var startDate = DateTime.UtcNow.Date;
            var endDate = DateTime.UtcNow.Date.AddDays(7);

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            
            var existingOverride = TestEntityFactory.CreateDoctorShiftOverride(overrideId, doctorId, hospitalId);
            existingOverride.StartDate = startDate;
            existingOverride.EndDate = endDate;
            existingOverride.ShiftName = "Morning";
            _context.DoctorShiftOverrides.Add(existingOverride);
            
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate,
                OverrideDate = DateTime.UtcNow.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning", // Should update
                        StartTime = "10:00",
                        EndTime = "13:00",
                        SlotDurationInMinutes = 45,
                        RecurringDays = new List<string> { "Tuesday" }
                    },
                     new ShiftDetails
                    {
                        ShiftName = "Evening", // Should add
                        StartTime = "17:00",
                        EndTime = "20:00",
                        SlotDurationInMinutes = 30,
                        RecurringDays = new List<string> { "Friday" }
                    }
                }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Doctor Override(s) updated: 1, added: 1"));
        }
    }
}
