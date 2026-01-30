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
    public class DoctorCreateHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorCreateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorCreateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesDoctorProfile()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var hospitalId = Guid.NewGuid();
            var hospitalUser = new HospitalUser { HospitalUserID = Guid.NewGuid(), UserID = user.UserID, HospitalID = hospitalId };
            _context.HospitalUsers.Add(hospitalUser);
            
            var dept = new Department { DepartmentID = Guid.NewGuid(), Name = "Cardiology" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var request = new DoctorCreateRequestModel
            {
                UserId = user.UserID,
                HospitalId = hospitalId,
                LicenseNumber = "LIC123",
                Qualification = new List<string> { "MBBS", "MD" },
                ExperienceYears = 10,
                PrimaryDepartment = "Cardiology",
                Department = "Cardiology",
                Specializations = new List<string> { "Heart Surgeon" }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.DoctorId, Is.Not.EqualTo(Guid.Empty));
            
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorID == response.DoctorId);
            Assert.That(doctor, Is.Not.Null);
            Assert.That(doctor.LicenseNumber, Is.EqualTo("LIC123"));
            Assert.That(doctor.Qualification, Does.Contain("MBBS"));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DoctorCreateRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }
    }
}
