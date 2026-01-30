using System;
using System.Collections.Generic;
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
    public class DoctorUpdateHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorUpdateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorUpdateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUpdate_UpdatesDoctorProfile()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospitalUser = new HospitalUser { HospitalUserID = Guid.NewGuid(), UserID = user.UserID, HospitalID = hospitalId };
            _context.HospitalUsers.Add(hospitalUser);
            await _context.SaveChangesAsync();

            var request = new DoctorUpdateRequestModel
            {
                UserId = user.UserID,
                Bio = "Updated Bio",
                ExperienceYears = 8
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var updatedDoctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorID == doctor.DoctorID);
            Assert.That(updatedDoctor.Bio, Is.EqualTo("Updated Bio"));
            Assert.That(updatedDoctor.ExperienceYears, Is.EqualTo(8));
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsFailure()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context); // User exists but no doctor profile
            var request = new DoctorUpdateRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found."));
        }
    }
}
