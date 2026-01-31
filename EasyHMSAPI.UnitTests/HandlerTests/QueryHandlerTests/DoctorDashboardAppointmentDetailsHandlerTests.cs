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
    public class DoctorDashboardAppointmentDetailsHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorDashboardAppointmentDetailsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorDashboardAppointmentDetailsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAppointments()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var dept = new Department { DepartmentID = Guid.NewGuid(), Name = "General" };
            _context.Departments.Add(dept);
            
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            doctor.PrimaryDepartmentID = dept.DepartmentID;
            
            var patient = new PatientRegistration { PatientId = "PAT1", FullName = "John Doe" };
            _context.PatientRegistrations.Add(patient);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT1",
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new DoctorDashboardAppointmentDetailsRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                Status = "All"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].PatientFullName, Is.EqualTo("John Doe"));
        }
    }
}
