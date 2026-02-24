using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DoctorOverrideCreateHandlerTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        private async Task<Guid> CreateTestDoctorAsync(Guid? userId = null)
        {
            var user = new User
            {
                UserID = userId ?? Guid.NewGuid(),
                MobileNumber = "1234567890",
                Email = "test@example.com",
                UserStatusId = 1
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var doctor = new Doctor
            {
                DoctorID = Guid.NewGuid(),
                UserID = user.UserID,
                LicenseNumber = "LICENSE123"
            };
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();
            return doctor.DoctorID;
        }

        [Test]
        public async Task Handle_WithValidRequest_ShouldCreateDoctorOverride()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);
            var hospitalId = Guid.NewGuid();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30,
                        RecurringDays = null
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("added: 1"));
        }

        [Test]
        public async Task Handle_WithInvalidDoctor_ShouldReturnFailure()
        {
            // Arrange
            var handler = new DoctorOverrideCreateHandler(_context);
            var invalidDoctorId = Guid.NewGuid();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = invalidDoctorId,
                HospitalId = Guid.NewGuid(),
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Invalid Doctor Id"));
        }

        [Test]
        public async Task Handle_WithInvalidShiftName_ShouldReturnFailure()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "InvalidShift",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Allowed values are Morning, Afternoon, Evening"));
        }

        [Test]
        public async Task Handle_WithDateRange_ShouldCreateRecordForEachDate()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);
            var hospitalId = Guid.NewGuid();
            var startDate = DateTime.Now.Date;
            var endDate = startDate.AddDays(2); // 3-day range

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = endDate,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("added: 3")); // 3 dates, 1 shift each
        }

        [Test]
        public async Task Handle_WithExistingOverride_ShouldUpdateAndAddNew()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);
            var hospitalId = Guid.NewGuid();
            var startDate = DateTime.Now.Date;

            // Create initial override for day 1
            var initialRequest = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = startDate,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };
            var initialResult = await handler.Handle(initialRequest, CancellationToken.None);
            Assert.That(initialResult.Success, Is.True);

            // Request for days 1-2 (day 1 exists, day 2 is new)
            var updateRequest = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = startDate,
                EndDate = startDate.AddDays(1),
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "10:00:00",
                        EndTime = "14:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(updateRequest, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("updated: 1"));
            Assert.That(result.Message, Contains.Substring("added: 1"));
        }

        [Test]
        public async Task Handle_WithMultipleShifts_ShouldCreateAllShifts()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);
            var hospitalId = Guid.NewGuid();

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "Morning",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    },
                    new ShiftDetails
                    {
                        ShiftName = "Afternoon",
                        StartTime = "13:00:00",
                        EndTime = "18:00:00",
                        SlotDurationInMinutes = 30
                    },
                    new ShiftDetails
                    {
                        ShiftName = "Evening",
                        StartTime = "18:00:00",
                        EndTime = "22:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("added: 3"));
        }

        [Test]
        public async Task Handle_WithNullShiftDetails_ShouldReturnSuccess()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = null
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("added: 0"));
        }

        [Test]
        public async Task Handle_WithWhitespaceShiftName_ShouldReturnFailure()
        {
            // Arrange
            var doctorId = await CreateTestDoctorAsync();
            var handler = new DoctorOverrideCreateHandler(_context);

            var request = new DoctorOverrideCreateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                StartDate = DateTime.Now.Date,
                EndDate = DateTime.Now.Date,
                OverrideDate = DateTime.Now.Date,
                ShiftDetails = new List<ShiftDetails>
                {
                    new ShiftDetails
                    {
                        ShiftName = "   ",
                        StartTime = "09:00:00",
                        EndTime = "13:00:00",
                        SlotDurationInMinutes = 30
                    }
                }
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Allowed values are Morning, Afternoon, Evening"));
        }
    }
}
