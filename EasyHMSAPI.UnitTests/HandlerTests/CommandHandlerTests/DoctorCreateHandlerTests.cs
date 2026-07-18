using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Implementations;
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
    public class DoctorCreateHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorCreateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorCreateHandler(_context, new SubscriptionLimitHelper(_context));
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
            var request = new DoctorCreateRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }

        [Test]
        public async Task Handle_DuplicateDoctor_ReturnsError()
        {
             // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(Guid.NewGuid(), userId));
            await _context.SaveChangesAsync();

            var request = new DoctorCreateRequestModel { UserId = userId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor profile already exists for this user."));
        }

        //[Test]
        //public async Task Handle_BasicCreation_ReturnsSuccess()
        //{
        //     // Arrange
        //    var userId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorCreateRequestModel
        //    {
        //        UserId = userId,
        //        LicenseNumber = "LIC123",
        //        Qualification = new List<string> { "MBBS", "MD" },
        //        ExperienceYears = 5,
        //        Bio = "Test Bio"
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
        //    Assert.That(response.Message, Is.EqualTo("Doctor profile created successfully."));
        //    Assert.That(response.DoctorId, Is.Not.EqualTo(Guid.Empty));

        //    var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserID == userId);
        //    Assert.That(doctor, Is.Not.Null);
        //    Assert.That(doctor!.LicenseNumber, Is.EqualTo("LIC123"));
        //    Assert.That(doctor.Qualification, Is.EqualTo("MBBS, MD"));
        //}

        //[Test]
        //public async Task Handle_DepartmentAssignment_ReturnsSuccess()
        //{
        //     // Arrange
        //    var userId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var deptId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    _context.Departments.Add(new Department { DepartmentID = deptId, Name = "General" });
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorCreateRequestModel
        //    {
        //        UserId = userId,
        //        Department = "General",
        //        HospitalId = hospitalId
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
            
        //    var docDept = await _context.DoctorDepartments.FirstOrDefaultAsync(dd => dd.DoctorID == response.DoctorId);
        //    Assert.That(docDept, Is.Not.Null);
        //    Assert.That(docDept!.DepartmentID, Is.EqualTo(deptId));
        //}

        //[Test]
        //public async Task Handle_Specializations_ReturnsSuccess()
        //{
        //     // Arrange
        //    var userId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var deptId = Guid.NewGuid();

        //    _context.Users.Add(TestEntityFactory.CreateUser(userId, (int)UserStatusEnum.Active));
        //    _context.HospitalUsers.Add(new HospitalUser { UserID = userId, HospitalID = hospitalId });
        //    _context.Departments.Add(new Department { DepartmentID = deptId, Name = "General" });
        //    await _context.SaveChangesAsync();

        //    var request = new DoctorCreateRequestModel
        //    {
        //        UserId = userId,
        //        Department = "General",
        //        Specializations = new List<string> { "Surgeon" },
        //        HospitalId = hospitalId
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);

        //    var docSpec = await _context.DoctorSpecializations.FirstOrDefaultAsync(ds => ds.DoctorID == response.DoctorId);
        //    Assert.That(docSpec, Is.Not.Null);
            
        //    var spec = await _context.Specializations.FindAsync(docSpec!.SpecializationID);
        //    Assert.That(spec!.Name, Is.EqualTo("Surgeon"));
        //}
    }
}
