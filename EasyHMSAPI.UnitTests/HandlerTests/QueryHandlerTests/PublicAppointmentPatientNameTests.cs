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
    /// <summary>The signed-in list says whose visit each one is; the ID-only guest lookup never does.</summary>
    [TestFixture]
    public class PublicAppointmentPatientNameTests
    {
        private AppDbContext _context = null!;
        private const string Mobile = "9876543210";

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Guid Seed(string patientId, string fullName)
        {
            var hospitalId = Guid.NewGuid();
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = patientId, FullName = fullName, Mobile = Mobile });
            var at = DateTime.Today.AddDays(2).AddHours(10);
            var appt = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = Guid.NewGuid(),
                PatientId = patientId,
                ApptDate = at.Date,
                StartAt = at,
                EndAt = at.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment,
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(appt);
            _context.SaveChanges();
            return appt.ApptId;
        }

        [Test]
        public async Task SignedInList_SaysWhoEachVisitIsFor()
        {
            var mine = Seed("PTID00000101", "Priya Kumari");
            var maa = Seed("PTID00000102", " Sunita Devi ");

            var res = await new GetPublicAppointmentsByMobileHandler(_context).Handle(new GetPublicAppointmentsByMobileRequestModel { Mobile = Mobile }, CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.Appointments.Find(a => a.AppointmentId == mine)!.PatientName, Is.EqualTo("Priya Kumari"));
            Assert.That(res.Appointments.Find(a => a.AppointmentId == maa)!.PatientName, Is.EqualTo("Sunita Devi"));
        }

        [Test]
        public async Task SignedInList_CarriesTheDoctorId()
        {
            var id = Seed("PTID00000104", "Priya Kumari");
            var doctorId = _context.Appointments.Single(a => a.ApptId == id).DoctorId;

            var res = await new GetPublicAppointmentsByMobileHandler(_context).Handle(new GetPublicAppointmentsByMobileRequestModel { Mobile = Mobile }, CancellationToken.None);

            Assert.That(res.Appointments.Find(a => a.AppointmentId == id)!.DoctorId, Is.EqualTo(doctorId));
        }

        [Test]
        public async Task GuestLookupById_NeverIncludesTheName()
        {
            var id = Seed("PTID00000103", "Priya Kumari");

            var res = await new GetPublicAppointmentHandler(_context).Handle(new GetPublicAppointmentRequestModel { AppointmentId = id }, CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.Appointment!.PatientName, Is.Null);
        }
    }
}
