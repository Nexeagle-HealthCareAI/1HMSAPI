using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PublicUpdatePatientAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private PublicUpdatePatientAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _smsServiceMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _handler = new PublicUpdatePatientAppointmentHandler(_context, _smsServiceMock.Object, new Mock<ILogger<PublicUpdatePatientAppointmentHandler>>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid apptId, string patientId) SeedActiveAppointment(string patientMobile = "9876543210", string statusCode = "FUTURE")
        {
            var hospitalUser = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Admin");
            var hospital = TestDataFactory.SeedHospital(_context, hospitalUser.UserID);

            var doctorUser = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Doctor");
            var doctor = TestDataFactory.SeedDoctor(_context, doctorUser);

            var patient = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospital.HospitalID,
                PatientId = $"PAT-{Guid.NewGuid():N}"[..12],
                FullName = "Test Patient",
                Mobile = patientMobile,
                Age = 30,
                Sex = "Female",
                GuardianName = "Old Guardian",
            };
            _context.PatientRegistrations.Add(patient);

            var apptId = Guid.NewGuid();
            var appt = new Appointment
            {
                ApptId = apptId,
                HospitalId = hospital.HospitalID,
                DoctorId = doctor.DoctorID,
                PatientId = patient.PatientId,
                ApptDate = DateTime.UtcNow.Date.AddDays(1),
                StartAt = DateTime.UtcNow.Date.AddDays(1).AddHours(10),
                EndAt = DateTime.UtcNow.Date.AddDays(1).AddHours(10).AddMinutes(15),
                CurrentStatusCode = statusCode,
            };
            _context.Appointments.Add(appt);
            _context.SaveChanges();
            return (apptId, patient.PatientId!);
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesOnlyProvidedFields()
        {
            var (apptId, patientId) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields { Age = 58 },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = _context.PatientRegistrations.First(p => p.PatientId == patientId);
            Assert.That(updated.Age, Is.EqualTo((short)58));
            // Untouched fields stay as seeded.
            Assert.That(updated.FullName, Is.EqualTo("Test Patient"));
            Assert.That(updated.Sex, Is.EqualTo("Female"));
            Assert.That(updated.GuardianName, Is.EqualTo("Old Guardian"));
        }

        [Test]
        public async Task Handle_AllFieldsProvided_UpdatesAll()
        {
            var (apptId, patientId) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields { FullName = "Aquib Khan", Age = 58, Gender = "male", Guardian = "Rajesh Khan" },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Patient!.FullName, Is.EqualTo("Aquib Khan"));
            Assert.That(response.Patient!.Age, Is.EqualTo((short)58));
            Assert.That(response.Patient!.Gender, Is.EqualTo("male"));
            Assert.That(response.Patient!.Guardian, Is.EqualTo("Rajesh Khan"));
        }

        [Test]
        public async Task Handle_MismatchedMobile_ReturnsGenericFailure()
        {
            var (apptId, _) = SeedActiveAppointment(patientMobile: "9876543210");

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "1111111111",
                    Patient = new PublicPatientUpdateFields { Age = 40 },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_NoFieldsProvided_ReturnsFailure()
        {
            var (apptId, _) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields(),
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("At least one field"));
        }

        [Test]
        public async Task Handle_AgeOutOfRange_ReturnsFailure()
        {
            var (apptId, _) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields { Age = 200 },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Age"));
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsFailure()
        {
            var (apptId, _) = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_Cancelled);

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields { Age = 40 },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cancelled"));
        }

        [Test]
        public async Task Handle_UnderConsultStatus_StillAllowsUpdate()
        {
            // Deliberately looser than PublicUpdateDoctorAppointmentHandler: correcting a name/age
            // typo mid-visit is harmless, unlike reassigning the doctor.
            var (apptId, patientId) = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_UnderConsult);

            var response = await _handler.Handle(
                new PublicUpdatePatientAppointmentRequestModel
                {
                    AppointmentId = apptId,
                    Mobile = "9876543210",
                    Patient = new PublicPatientUpdateFields { Age = 45 },
                },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = _context.PatientRegistrations.First(p => p.PatientId == patientId);
            Assert.That(updated.Age, Is.EqualTo((short)45));
        }
    }
}
