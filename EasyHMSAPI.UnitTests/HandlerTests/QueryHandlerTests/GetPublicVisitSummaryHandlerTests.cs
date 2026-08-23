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
    public class GetPublicVisitSummaryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicVisitSummaryHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicVisitSummaryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_EmptyId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicVisitSummaryRequestModel { AppointmentId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicVisitSummaryRequestModel { AppointmentId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AppointmentHasNoPdfUrl_ReturnsFailure()
        {
            var apptId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment { ApptId = apptId, HospitalId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), PatientId = "PAT1", ApptDate = DateTime.UtcNow, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddMinutes(15), LastStatusCodeAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicVisitSummaryRequestModel { AppointmentId = apptId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No prescription"));
        }

        [Test]
        public async Task Handle_AppointmentHasPdfUrl_ReturnsRedirectUrl()
        {
            var apptId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment { ApptId = apptId, HospitalId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), PatientId = "PAT1", ApptDate = DateTime.UtcNow, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddMinutes(15), LastStatusCodeAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, PdfUrl = "http://storage.example.com/rx.pdf" });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicVisitSummaryRequestModel { AppointmentId = apptId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RedirectUrl, Is.EqualTo("http://storage.example.com/rx.pdf"));
        }
    }
}
