using System;
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
    public class GetHospitalOverallAnalysisHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalOverallAnalysisHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalOverallAnalysisHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAnalysis()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT1",
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_New
            };
            _context.Appointments.Add(appointment);
            
             var patient = new PatientRegistration 
             { 
                 PatientId = "PAT1", 
                 HospitalId = hospitalId, 
                 FullName = "John Doe",
                 Sex = "Male",
                 AgeYears = 30
             };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

            var request = new GetHospitalOverallAnalysisRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data.Kpis.TotalVisits.Overall, Is.EqualTo(1));
            Assert.That(response.Data.Overall.AgeDistribution["21-30"], Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new GetHospitalOverallAnalysisRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Hospital not found."));
        }
    }
}
