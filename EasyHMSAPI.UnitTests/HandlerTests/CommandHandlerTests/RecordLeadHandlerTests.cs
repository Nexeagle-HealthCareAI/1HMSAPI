using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RecordLeadHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IGeoIpLookupService> _geoIpLookupServiceMock = null!;
        private RecordLeadHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _geoIpLookupServiceMock = new Mock<IGeoIpLookupService>();
            _geoIpLookupServiceMock
                .Setup(g => g.LookupAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoIpResult("India", "West Bengal", "Kolkata"));

            _handler = new RecordLeadHandler(_context, _geoIpLookupServiceMock.Object, new Mock<ILogger<RecordLeadHandler>>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private static RecordLeadRequestModel ValidRequest(Guid? hospitalId = null) => new()
        {
            HospitalId = hospitalId ?? Guid.NewGuid(),
            Source = "DoctorDekho",
            LeadType = "DoctorProfileView",
        };

        [Test]
        public async Task Handle_EmptyHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(ValidRequest(Guid.Empty), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HospitalLeads, Is.Empty);
        }

        [Test]
        public async Task Handle_UnknownSource_ReturnsFailure()
        {
            var request = ValidRequest();
            request.Source = "SomeOtherChannel";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HospitalLeads, Is.Empty);
        }

        [Test]
        public async Task Handle_UnknownLeadType_ReturnsFailure()
        {
            var request = ValidRequest();
            request.LeadType = "SomethingMadeUp";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HospitalLeads, Is.Empty);
        }

        [Test]
        public async Task Handle_ValidWebLead_PersistsWithGeoResolved()
        {
            var hospitalId = Guid.NewGuid();
            var request = ValidRequest(hospitalId);
            request.IpAddress = "1.2.3.4";
            request.SearchQuery = "Dr. Priya Sharma";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.HospitalLeads.Count(), Is.EqualTo(1));
            var lead = _context.HospitalLeads.First();
            Assert.That(lead.HospitalId, Is.EqualTo(hospitalId));
            Assert.That(lead.Source, Is.EqualTo("DoctorDekho"));
            Assert.That(lead.LeadType, Is.EqualTo("DoctorProfileView"));
            Assert.That(lead.City, Is.EqualTo("Kolkata"));
        }

        [Test]
        public async Task Handle_DemoLoginLead_PersistsWithPatientNameAndMobile()
        {
            var hospitalId = Guid.NewGuid();
            var request = ValidRequest(hospitalId);
            request.Source = "1HMSDemo";
            request.LeadType = "DemoLogin";
            request.PatientName = "Dr. Test Doctor";
            request.Mobile = "919876543210";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var lead = _context.HospitalLeads.First();
            Assert.That(lead.Source, Is.EqualTo("1HMSDemo"));
            Assert.That(lead.LeadType, Is.EqualTo("DemoLogin"));
            Assert.That(lead.PatientName, Is.EqualTo("Dr. Test Doctor"));
        }

        [Test]
        public async Task Handle_WhatsAppLead_NoIpAddress_SkipsGeoLookup()
        {
            var request = ValidRequest();
            request.Source = "WhatsApp";
            request.LeadType = "DoctorNameSearch";
            request.Mobile = "919876543210";
            // IpAddress deliberately left unset -- the bot has no visitor IP to forward.

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var lead = _context.HospitalLeads.First();
            Assert.That(lead.Mobile, Is.EqualTo("919876543210"));
            Assert.That(lead.City, Is.Null);
            _geoIpLookupServiceMock.Verify(g => g.LookupAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
