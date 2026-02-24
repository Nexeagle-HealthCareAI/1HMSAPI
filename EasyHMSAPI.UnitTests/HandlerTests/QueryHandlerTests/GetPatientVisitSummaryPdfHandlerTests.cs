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
    public class GetPatientVisitSummaryPdfHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPatientVisitSummaryPdfHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPatientVisitSummaryPdfHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsPdfUrl()
        {
            // Arrange
            var apptId = Guid.NewGuid();
            var appointment = new Appointment
            {
                ApptId = apptId,
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                PdfUrl = "http://example.com/summary.pdf"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new GetPatientVisitSummaryPdfRequestModel { AppointmentId = apptId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.PdfUrl, Is.EqualTo("http://example.com/summary.pdf"));
        }

        [Test]
        public async Task Handle_NotCompleted_ReturnsError()
        {
             // Arrange
            var apptId = Guid.NewGuid();
             var appointment = new Appointment
            {
                ApptId = apptId,
                CurrentStatusCode = "Booked",
                PdfUrl = "http://example.com/summary.pdf"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new GetPatientVisitSummaryPdfRequestModel { AppointmentId = apptId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not completed"));
        }
    }
}
