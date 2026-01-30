using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPrescriptionDetailsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GetPrescriptionDetailsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
             _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GetPrescriptionDetailsHandler(_context, _doctorValidationHelperMock.Object);
        }

         [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsPrescriptionDetails()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var apptId = Guid.NewGuid();
            var patientId = "PAT1";

            var appointment = new Appointment
            {
                ApptId = apptId,
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.UtcNow
            };
            _context.Appointments.Add(appointment);

            var prescription = new Prescription
            {
                PrescriptionId = Guid.NewGuid(),
                ApptId = apptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = patientId,
                Diagnosis = "Flu"
            };
            _context.Prescription.Add(prescription);

            var investigation = new PrescriptionInvestigation
            {
                PrescriptionId = prescription.PrescriptionId,
                OrdersType = AppConstants.LookupType_Investigation,
                Name = "Blood Test"
            };
            _context.PrescriptionInvestigation.Add(investigation);
             await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPrescriptionDetailsRequestModel
            {
                 AppointmentId = apptId,
                 PatientId = patientId,
                 HospitalId = hospitalId,
                 DoctorId = doctor.DoctorID
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.Diagnosis, Is.EqualTo("Flu"));
            Assert.That(response.Data.Orders.Investigations[0], Is.EqualTo("Blood Test"));
        }
    }
}
