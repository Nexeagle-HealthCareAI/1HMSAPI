using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Enums;
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
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsError()
        {
            // Arrange
            var request = new DoctorUpdateRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found."));
        }

        [Test]
        public async Task Handle_ValidUpdate_PersistsLanguagesAndPublicContactFields()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var request = new DoctorUpdateRequestModel
            {
                UserId = user.UserID,
                Languages = new List<string> { "English", "Hindi" },
                PublicContactEmail = "doctor@example.com",
                PublicContactPhone = "9876543210",
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.UpdatedFields, Does.Contain("Languages"));
            Assert.That(response.UpdatedFields, Does.Contain("PublicContactEmail"));
            Assert.That(response.UpdatedFields, Does.Contain("PublicContactPhone"));

            var updated = await _context.Doctors.FindAsync(doctor.DoctorID);
            Assert.That(updated!.LanguagesJson, Is.EqualTo("[\"English\",\"Hindi\"]"));
            Assert.That(updated.PublicContactEmail, Is.EqualTo("doctor@example.com"));
            Assert.That(updated.PublicContactPhone, Is.EqualTo("9876543210"));
        }

        [Test]
        public async Task Handle_AdminEditingDoctorNotAtSpecifiedHospital_RejectsUpdate()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var otherHospitalId = Guid.NewGuid();
            // No DoctorDepartment row links this doctor to otherHospitalId.

            var request = new DoctorUpdateRequestModel
            {
                UserId = user.UserID,
                HospitalId = otherHospitalId,
                Bio = "Should not be applied",
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found at this hospital."));

            var unchanged = await _context.Doctors.FindAsync(doctor.DoctorID);
            Assert.That(unchanged!.Bio, Is.EqualTo(doctor.Bio));
        }

        [Test]
        public async Task Handle_AdminEditingDoctorAtOwnHospital_AllowsUpdate()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);

            var request = new DoctorUpdateRequestModel
            {
                UserId = user.UserID,
                HospitalId = hospital.HospitalID,
                Bio = "Updated by admin",
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var updated = await _context.Doctors.FindAsync(doctor.DoctorID);
            Assert.That(updated!.Bio, Is.EqualTo("Updated by admin"));
        }

        //[Test]
        //public async Task Handle_BasicProfileUpdate_ReturnsSuccess()
        //{
        //    // Arrange
        //    var userId = Guid.NewGuid();
        //    var doctorId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    _context.Doctors.Add(new Doctor 
        //    { 
        //        DoctorID = doctorId, 
        //        UserID = userId,
        //        Qualification = "MBBS"
        //    });
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorUpdateRequestModel
        //    {
        //        UserId = userId,
        //        LicenseNumber = "LIC123",
        //        ExperienceYears = 5,
        //        Bio = "Experienced Doctor"
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
        //    Assert.That(response.UpdatedFields, Does.Contain("LicenseNumber"));
        //    Assert.That(response.UpdatedFields, Does.Contain("ExperienceYears"));
        //    Assert.That(response.UpdatedFields, Does.Contain("Bio"));

        //    var doctor = await _context.Doctors.FindAsync(doctorId);
        //    Assert.That(doctor!.LicenseNumber, Is.EqualTo("LIC123"));
        //    Assert.That(doctor.ExperienceYears, Is.EqualTo(5));
        //    Assert.That(doctor.Bio, Is.EqualTo("Experienced Doctor"));
        //}

        //[Test]
        //public async Task Handle_DepartmentUpdate_ReturnsSuccess()
        //{
        //     // Arrange
        //    var userId = Guid.NewGuid();
        //    var doctorId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var deptId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
        //    _context.Departments.Add(new Department { DepartmentID = deptId, Name = "Cardiology" });
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorUpdateRequestModel
        //    {
        //        UserId = userId,
        //        Department = "Cardiology",
        //        PrimaryDepartment = "Cardiology"
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
        //    Assert.That(response.UpdatedFields, Does.Contain("Department"));
            
        //    var docDept = await _context.DoctorDepartments.FirstOrDefaultAsync(dd => dd.DoctorID == doctorId);
        //    Assert.That(docDept, Is.Not.Null);
        //    Assert.That(docDept!.DepartmentID, Is.EqualTo(deptId));
        //}

        //[Test]
        //public async Task Handle_SpecializationUpdate_ReturnsSuccess()
        //{
        //     // Arrange
        //    var userId = Guid.NewGuid();
        //    var doctorId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var deptId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
        //    _context.Departments.Add(new Department { DepartmentID = deptId, Name = "Cardiology" });
        //    // Existing department assignment needed if not passed in request, but test passes dept in request to keep it simple
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorUpdateRequestModel
        //    {
        //        UserId = userId,
        //        Department = "Cardiology",
        //        Specializations = new List<string> { "Heart Surgeon", "Cardiologist" }
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
        //    Assert.That(response.UpdatedFields, Does.Contain("DoctorSpecializations"));
            
        //    var specializations = await _context.DoctorSpecializations.Where(ds => ds.DoctorID == doctorId).ToListAsync();
        //    Assert.That(specializations.Count, Is.EqualTo(2));
        //}
    }
}
