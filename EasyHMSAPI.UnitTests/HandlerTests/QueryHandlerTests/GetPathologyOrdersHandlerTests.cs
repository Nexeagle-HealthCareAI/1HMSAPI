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
    // Covers the dashboard-list-only fields (TestCount, ReportNo, ReportGeneratedAt,
    // ReportPdfBlobPath) added for the Pathology Lab dashboard table -- these are correlated
    // subqueries in the handler's projection, worth a real test rather than assuming EF Core
    // translates them as expected.
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
        public async Task Handle_OrderWithNoReportYet_ReportFieldsAreNullAndTestCountReflectsLines()
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
            Assert.That(dto.ReportNo, Is.Null);
            Assert.That(dto.ReportGeneratedAt, Is.Null);
            Assert.That(dto.ReportPdfBlobPath, Is.Null);
        }

        [Test]
        public async Task Handle_OrderWithGeneratedReport_ExposesReportSummaryFields()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var generatedAt = DateTime.UtcNow;
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-2",
                Status = "COMPLETED",
            });
            _context.PathologyReport.Add(new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportNo = "LR-1",
                Status = "GENERATED",
                GeneratedAt = generatedAt,
                PdfBlobPath = "https://blob.example/report.pdf",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrdersQuery { HospitalId = hospitalId }, CancellationToken.None);

            var dto = result.Single(o => o.OrderId == orderId);
            Assert.That(dto.TestCount, Is.EqualTo(0));
            Assert.That(dto.ReportNo, Is.EqualTo("LR-1"));
            Assert.That(dto.ReportGeneratedAt, Is.EqualTo(generatedAt));
            Assert.That(dto.ReportPdfBlobPath, Is.EqualTo("https://blob.example/report.pdf"));
        }
    }
}
