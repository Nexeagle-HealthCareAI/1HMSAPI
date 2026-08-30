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
    public class GetPathologyReportVerificationHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPathologyReportVerificationHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPathologyReportVerificationHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private PathologyReport SeedApprovedReport(Guid hospitalId, string sha256)
        {
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-1",
                Status = "APPROVED",
                ApprovedAt = DateTime.UtcNow,
                TechnicianName = "Ravi Technician",
                PathologistName = "Dr. Asha Rao",
                PdfSha256 = sha256,
            };
            _context.PathologyReport.Add(report);
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId,
                Name = "City Care Hospital",
                Type = "GENERAL",
                RegistrationNumber = "REG-1",
                Contact = "9999999999",
                Location = "Test Location",
                City = "Test City",
                State = "Test State",
                Country = "India",
                Pincode = "000000",
            });
            _context.SaveChanges();
            return report;
        }

        [Test]
        public async Task Handle_NoHashSupplied_ReturnsAuthenticBasicCheck()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedApprovedReport(hospitalId, "abc123");

            var response = await _handler.Handle(new GetPathologyReportVerificationQuery
            {
                ReportId = report.ReportId,
                Sha256 = null,
            }, CancellationToken.None);

            Assert.That(response.IsAuthentic, Is.True);
            Assert.That(response.ReportNo, Is.EqualTo("LR-1"));
            Assert.That(response.HospitalName, Is.EqualTo("City Care Hospital"));
        }

        [Test]
        public async Task Handle_MatchingHashSupplied_ReturnsAuthenticStrictCheck()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedApprovedReport(hospitalId, "abc123");

            var response = await _handler.Handle(new GetPathologyReportVerificationQuery
            {
                ReportId = report.ReportId,
                Sha256 = "ABC123",
            }, CancellationToken.None);

            Assert.That(response.IsAuthentic, Is.True);
        }

        [Test]
        public async Task Handle_MismatchedHashSupplied_ReturnsNotAuthentic()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedApprovedReport(hospitalId, "abc123");

            var response = await _handler.Handle(new GetPathologyReportVerificationQuery
            {
                ReportId = report.ReportId,
                Sha256 = "totally-different-hash",
            }, CancellationToken.None);

            Assert.That(response.IsAuthentic, Is.False);
        }

        [Test]
        public async Task Handle_UnknownReportId_ReturnsNotAuthentic()
        {
            var response = await _handler.Handle(new GetPathologyReportVerificationQuery
            {
                ReportId = Guid.NewGuid(),
                Sha256 = null,
            }, CancellationToken.None);

            Assert.That(response.IsAuthentic, Is.False);
        }

        [Test]
        public async Task Handle_ReportNotYetApproved_ReturnsNotAuthentic()
        {
            var hospitalId = Guid.NewGuid();
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-2",
                Status = "TECH_SIGNED",
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();

            var response = await _handler.Handle(new GetPathologyReportVerificationQuery
            {
                ReportId = report.ReportId,
                Sha256 = null,
            }, CancellationToken.None);

            Assert.That(response.IsAuthentic, Is.False);
        }
    }
}
