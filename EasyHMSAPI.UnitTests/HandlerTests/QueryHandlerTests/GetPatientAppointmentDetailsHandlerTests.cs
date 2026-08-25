using System;
using System.Linq;
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

        [Test]
        public async Task Handle_WithMultipleDoctorsAndNoDoctorFilter_MatchesEachAppointmentToItsOwnDoctor()
        {
            // Regression test for the doctor/department lookup: it used to rejoin Appointments
            // via a Contains() over every fetched ApptId; now it's keyed by distinct DoctorId
            // instead. This exercises exactly the shape that could silently mismatch doctors to
            // appointments if that lookup were keyed or joined incorrectly -- multiple
            // appointments across multiple distinct doctors, with no doctorId/patientId filter on
            // the request (the same request shape reported as slow: hospital + date range only).
            var hospitalId = Guid.NewGuid();

            var userA = TestDataFactory.SeedUser(_context, email: "doc-a@example.com", phone: "1111111111", role: "Doctor");
            var doctorA = TestDataFactory.SeedDoctor(_context, userA);
            _context.UserProfiles.Add(new UserProfile { UserID = userA.UserID, FullName = "Dr. Alpha" });

            var userB = TestDataFactory.SeedUser(_context, email: "doc-b@example.com", phone: "2222222222", role: "Doctor");
            var doctorB = TestDataFactory.SeedDoctor(_context, userB);
            _context.UserProfiles.Add(new UserProfile { UserID = userB.UserID, FullName = "Dr. Beta" });

            var patientA = new PatientRegistration { PatientId = "PAT-A", FullName = "Patient A" };
            var patientB = new PatientRegistration { PatientId = "PAT-B", FullName = "Patient B" };
            _context.PatientRegistrations.AddRange(patientA, patientB);

            var apptA = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientA.PatientId,
                DoctorId = doctorA.DoctorID,
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed"
            };
            var apptB = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientB.PatientId,
                DoctorId = doctorB.DoctorID,
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed"
            };
            _context.Appointments.AddRange(apptA, apptB);
            await _context.SaveChangesAsync();

            // Same request shape reported as slow in prod: hospitalId + date range, no doctorId.
            var request = new GetPatientAppointmentDetailsRequestModel
            {
                HospitalId = hospitalId,
                Status = "All",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(2));
            var resultA = response.Items.Single(i => i.AppointmentId == apptA.ApptId);
            var resultB = response.Items.Single(i => i.AppointmentId == apptB.ApptId);
            Assert.That(resultA.DoctorName, Is.EqualTo("Dr. Alpha"));
            Assert.That(resultB.DoctorName, Is.EqualTo("Dr. Beta"));
        }
    }
}
