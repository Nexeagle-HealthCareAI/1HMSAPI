using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPatientsByHospitalIdHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPatientsByHospitalIdHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPatientsByHospitalIdHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsPatientsAndStats()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Dr. One" };
            _context.UserProfiles.Add(userProfile);

            var patient = new PatientRegistration 
            { 
                 PatientId = "PAT1", 
                 HospitalId = hospitalId, 
                 FullName = "Patient One",
                 Sex = AppConstants.PatientSex_Male,
                 RegisteredAt = DateTime.UtcNow
            };
            _context.PatientRegistrations.Add(patient);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT1",
                ApptDate = DateTime.Today
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new GetPatientsByHospitalIdRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.PatientsData, Has.Count.EqualTo(1));
            Assert.That(response.PatientsData[0].Name, Is.EqualTo("Patient One"));
            Assert.That(response.Statistics.TotalPatientCount, Is.EqualTo(1));
            Assert.That(response.DoctorsData, Has.Count.EqualTo(1));
            Assert.That(response.DoctorsData[0].DoctorName, Is.EqualTo("Dr. One"));
        }
    }
}
