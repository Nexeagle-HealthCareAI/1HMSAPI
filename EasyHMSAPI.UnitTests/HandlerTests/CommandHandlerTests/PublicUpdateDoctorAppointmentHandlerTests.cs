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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PublicUpdateDoctorAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private IMemoryCache _cache = null!;
        private PublicUpdateDoctorAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _smsServiceMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _handler = new PublicUpdateDoctorAppointmentHandler(_context, _smsServiceMock.Object, new Mock<ILogger<PublicUpdateDoctorAppointmentHandler>>().Object, _cache);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
            _cache.Dispose();
        }

        private (Guid apptId, Guid hospitalId, Guid originalDoctorId) SeedActiveAppointment(string patientMobile = "9876543210", string statusCode = "FUTURE")
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
            return (apptId, hospital.HospitalID, doctor.DoctorID);
        }

        private Guid SeedPubliclyListedDoctor(Guid hospitalId)
        {
            var user = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Doctor");
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospitalId);
            return doctor.DoctorID;
        }

        [Test]
        public async Task Handle_ValidRequest_MovesDoctorSuccessfully()
        {
            var (apptId, hospitalId, _) = SeedActiveAppointment();
            var newDoctorId = SeedPubliclyListedDoctor(hospitalId);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = newDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(updated.DoctorId, Is.EqualTo(newDoctorId));
        }

        [Test]
        public async Task Handle_MismatchedMobile_ReturnsGenericFailure()
        {
            var (apptId, hospitalId, _) = SeedActiveAppointment(patientMobile: "9876543210");
            var newDoctorId = SeedPubliclyListedDoctor(hospitalId);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "1111111111", NewDoctorId = newDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_UnknownAppointmentId_ReturnsFailure()
        {
            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = Guid.NewGuid(), Mobile = "9876543210", NewDoctorId = Guid.NewGuid() },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsFailure()
        {
            var (apptId, hospitalId, _) = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_Cancelled);
            var newDoctorId = SeedPubliclyListedDoctor(hospitalId);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = newDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cancelled"));
        }

        [Test]
        public async Task Handle_InProgressAppointment_ReturnsFailure()
        {
            var (apptId, hospitalId, _) = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_UnderConsult);
            var newDoctorId = SeedPubliclyListedDoctor(hospitalId);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = newDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("in progress"));
        }

        [Test]
        public async Task Handle_SameDoctor_ReturnsFailure()
        {
            var (apptId, _, originalDoctorId) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = originalDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already booked"));
        }

        [Test]
        public async Task Handle_DoctorNotPubliclyListed_ReturnsFailure()
        {
            var (apptId, hospitalId, _) = SeedActiveAppointment();

            // Seeded with isPubliclyListed defaulting to false and no DoctorDepartments row, so it
            // can't resolve via ResolvePubliclyListedHospitalIdAsync.
            var notPublicUser = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Doctor");
            var notPublicDoctor = TestDataFactory.SeedDoctor(_context, notPublicUser);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = notPublicDoctor.DoctorID },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found."));
        }

        [Test]
        public async Task Handle_DoctorAtDifferentHospital_ReturnsFailure()
        {
            var (apptId, _, _) = SeedActiveAppointment();

            var otherHospitalUser = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Admin");
            var otherHospital = TestDataFactory.SeedHospital(_context, otherHospitalUser.UserID);
            var newDoctorId = SeedPubliclyListedDoctor(otherHospital.HospitalID);

            var response = await _handler.Handle(
                new PublicUpdateDoctorAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", NewDoctorId = newDoctorId },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found for this hospital."));
        }
    }
}
