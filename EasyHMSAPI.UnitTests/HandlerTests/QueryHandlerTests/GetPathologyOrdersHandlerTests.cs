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
    // Covers the dashboard-list-only fields (TestCount, ReportsReadyCount) added for the Pathology
    // Lab dashboard table -- these are correlated subqueries in the handler's projection, worth a
    // real test rather than assuming EF Core translates them as expected. ReportsReadyCount counts
    // lines with a ReportId rather than picking a single report, since each PathologyOrderLine now
    // gets its own independent report (see GeneratePathologyReportHandler).
    [TestFixture]
    public class GetPathologyOrdersHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPathologyOrdersHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPathologyOrdersHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_OrderWithNoReportsYet_ReportsReadyCountIsZeroAndTestCountReflectsLines()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = "IN_PROGRESS",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "PENDING",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "PENDING",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrdersQuery { HospitalId = hospitalId }, CancellationToken.None);

            var dto = result.Single(o => o.OrderId == orderId);
            Assert.That(dto.TestCount, Is.EqualTo(2));
            Assert.That(dto.ReportsReadyCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_OrderWithOneOfTwoLinesReported_ReportsReadyCountReflectsOnlyThatLine()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-2",
                Status = "IN_PROGRESS",
            });
            _context.PathologyReport.Add(new PathologyReport
            {
                ReportId = reportId,
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportNo = "LR-1",
                Status = "GENERATED",
                GeneratedAt = DateTime.UtcNow,
                PdfBlobPath = "https://blob.example/report.pdf",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "RESULT_ENTERED",
                ReportId = reportId,
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "PENDING",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrdersQuery { HospitalId = hospitalId }, CancellationToken.None);

            var dto = result.Single(o => o.OrderId == orderId);
            Assert.That(dto.TestCount, Is.EqualTo(2));
            Assert.That(dto.ReportsReadyCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_OrderWithTokenAndTests_ExposesTokenNumberTestNamesAndPatientInfo()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var testOneId = Guid.NewGuid();
            var testTwoId = Guid.NewGuid();

            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                FullName = "Rajesh Kumar",
                Mobile = "9876543210",
                DateOfBirth = DateTime.UtcNow.AddYears(-40),
            });
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-3",
                Status = "IN_PROGRESS",
                TokenNumber = 7,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testOneId, HospitalId = hospitalId, TestCode = "HEM-CBC", TestName = "CBC", IsActive = true,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testTwoId, HospitalId = hospitalId, TestCode = "BIO-LFT", TestName = "LFT", IsActive = true,
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(), HospitalId = hospitalId, OrderId = orderId, TestId = testOneId, Status = "PENDING",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(), HospitalId = hospitalId, OrderId = orderId, TestId = testTwoId, Status = "PENDING",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrdersQuery { HospitalId = hospitalId }, CancellationToken.None);

            var dto = result.Single(o => o.OrderId == orderId);
            Assert.That(dto.TokenNumber, Is.EqualTo(7));
            Assert.That(dto.PatientMobile, Is.EqualTo("9876543210"));
            Assert.That(dto.PatientAgeYears, Is.EqualTo(40));
            Assert.That(dto.TestNames, Is.EquivalentTo(new[] { "CBC", "LFT" }));
        }
    }
}
