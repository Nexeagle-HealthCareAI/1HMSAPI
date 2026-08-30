using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SignPathologyReportAsTechnicianHandlerTests
    {
        private AppDbContext _context = null!;
        private SignPathologyReportAsTechnicianHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SignPathologyReportAsTechnicianHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private PathologyReport SeedDraftReport()
        {
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-1",
                Status = "DRAFT",
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();
            return report;
        }

        [Test]
        public async Task Handle_DraftReport_TransitionsToTechSignedAndCapturesIdentity()
        {
            var report = SeedDraftReport();
            var techUserId = Guid.NewGuid();

            var result = await _handler.Handle(new SignPathologyReportAsTechnicianCommand
            {
                HospitalId = report.HospitalId,
                ReportId = report.ReportId,
                TechnicianRegNo = "DMLT-12345",
                LoggedInUserId = techUserId,
                LoggedInUserName = "Ravi Technician",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyReport.Single(r => r.ReportId == report.ReportId);
            Assert.That(saved.Status, Is.EqualTo("TECH_SIGNED"));
            Assert.That(saved.TechnicianUserId, Is.EqualTo(techUserId));
            Assert.That(saved.TechnicianName, Is.EqualTo("Ravi Technician"));
            Assert.That(saved.TechnicianRegNo, Is.EqualTo("DMLT-12345"));
            Assert.That(saved.TechnicianSignedAt, Is.Not.Null);
        }

        [Test]
        public void Handle_AlreadySignedReport_Throws()
        {
            var report = SeedDraftReport();
            report.Status = "TECH_SIGNED";
            _context.SaveChanges();

            Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new SignPathologyReportAsTechnicianCommand
            {
                HospitalId = report.HospitalId,
                ReportId = report.ReportId,
                TechnicianRegNo = "DMLT-12345",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None));
        }

        [Test]
        public void Handle_MissingRegNo_Throws()
        {
            var report = SeedDraftReport();

            Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new SignPathologyReportAsTechnicianCommand
            {
                HospitalId = report.HospitalId,
                ReportId = report.ReportId,
                TechnicianRegNo = "  ",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None));
        }

        [Test]
        public void Handle_UnknownReport_Throws()
        {
            Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new SignPathologyReportAsTechnicianCommand
            {
                HospitalId = Guid.NewGuid(),
                ReportId = Guid.NewGuid(),
                TechnicianRegNo = "DMLT-12345",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None));
        }
    }
}
