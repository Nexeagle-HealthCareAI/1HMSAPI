using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDepartmentDoctorsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDepartmentDoctorsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDepartmentDoctorsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsDoctors()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Dr. Test" };
            _context.UserProfiles.Add(userProfile);

            var deptId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            
            var docDept = new DoctorDepartment 
            { 
                DoctorDepartmentID = Guid.NewGuid(), 
                DoctorID = doctor.DoctorID, 
                DepartmentID = deptId,
                HospitalId = hospitalId 
            };
            _context.DoctorDepartments.Add(docDept);

            var spec = new Specialization { SpecializationID = Guid.NewGuid(), Name = "Spec1", IsActive = true };
            _context.Specializations.Add(spec);
            
            var docSpec = new DoctorSpecialization { DoctorSpecializationID = Guid.NewGuid(), DoctorID = doctor.DoctorID, SpecializationID = spec.SpecializationID };
            _context.DoctorSpecializations.Add(docSpec);

            await _context.SaveChangesAsync();

            var request = new GetDepartmentDoctorsRequestModel { DepartmentId = deptId, HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorName, Is.EqualTo("Dr. Test"));
            Assert.That(response.Doctors[0].Specializations, Does.Contain("Spec1"));
        }
    }
}
