using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPatientAnalysisHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GetPatientAnalysisHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GetPatientAnalysisHandler(_context, _doctorValidationHelperMock.Object);
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
            var patientId = "PAT123";
            var patient = new PatientRegistration { PatientId = patientId, HospitalId = hospitalId, FullName = "John" };
            _context.PatientRegistrations.Add(patient);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                PatientId = patientId,
                HospitalId = hospitalId,
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new GetPatientAnalysisRequestModel { HospitalId = hospitalId, PatientId = patientId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.PatientAnalysis.TotalVisit, Is.EqualTo(1));
            Assert.That(response.PatientAnalysis.PatientTags, Does.Contain("New Patient"));
        }

        [Test]
        public async Task Handle_PatientNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new GetPatientAnalysisRequestModel { HospitalId = Guid.NewGuid(), PatientId = "UNKNOWN" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Patient not found."));
        }
    }
}
