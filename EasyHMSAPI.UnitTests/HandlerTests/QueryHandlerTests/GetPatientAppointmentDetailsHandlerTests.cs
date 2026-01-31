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
    public class GetPatientAppointmentDetailsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPatientAppointmentDetailsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPatientAppointmentDetailsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAppointmentDetails()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var patientId = "PAT1";
            var patient = new PatientRegistration { PatientId = patientId, FullName = "John Doe" };
            _context.PatientRegistrations.Add(patient);
            
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Dr. Test" };
            _context.UserProfiles.Add(userProfile);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                DoctorId = doctor.DoctorID,
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new GetPatientAppointmentDetailsRequestModel { HospitalId = hospitalId, Status = "All" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].DoctorName, Is.EqualTo("Dr. Test"));
            Assert.That(response.Items[0].PatientFullName, Is.EqualTo("John Doe"));
        }
    }
}
