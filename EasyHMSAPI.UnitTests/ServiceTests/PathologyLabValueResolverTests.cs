using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    // Covers the shared resolver behind SOFA/APACHE II auto-fill's new lab-value fields --
    // GetSofaAutoFillHandlerTests/GetApacheIIAutoFillHandlerTests cover the handler wiring on top
    // of this; these tests focus on the resolver's own JSON-parsing and report-selection logic.
    [TestFixture]
    public class PathologyLabValueResolverTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private PathologyReport SeedApprovedReportWithResult(
            Guid hospitalId, string patientId, string resultValuesJson, DateTime approvedAt)
        {
            var orderId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();

            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = patientId,
                OrderNo = "ORD-" + orderId,
                Status = "COMPLETED",
            });
            var report = new PathologyReport
            {
                ReportId = reportId,
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportNo = "LR-" + reportId,
                Status = "APPROVED",
                ApprovedAt = approvedAt,
            };
            _context.PathologyReport.Add(report);
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = orderLineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "REPORT_APPROVED",
                ReportId = reportId,
            });
            _context.PathologyResult.Add(new PathologyResult
            {
                ResultId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderLineId = orderLineId,
                ReportId = reportId,
                ResultValuesJson = resultValuesJson,
            });
            _context.SaveChanges();
            return report;
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_EnrichedShape_ExtractsNumericValues()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-1";
            var approvedAt = DateTime.UtcNow;
            SeedApprovedReportWithResult(hospitalId, patientId,
                "{\"Serum Creatinine\":{\"value\":\"0.95\",\"flag\":\"NORMAL\"},\"Serum Sodium (Na+)\":{\"value\":\"138.0\",\"flag\":\"NORMAL\"}}",
                approvedAt);

            var (values, resultApprovedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, hospitalId, patientId, CancellationToken.None);

            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Creatinine"), Is.EqualTo(0.95m));
            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Sodium (Na+)"), Is.EqualTo(138.0m));
            Assert.That(resultApprovedAt, Is.EqualTo(approvedAt));
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_MultipleApprovedReports_PicksMostRecent()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-2";
            SeedApprovedReportWithResult(hospitalId, patientId,
                "{\"Serum Creatinine\":{\"value\":\"1.10\",\"flag\":\"NORMAL\"}}", DateTime.UtcNow.AddDays(-5));
            SeedApprovedReportWithResult(hospitalId, patientId,
                "{\"Serum Creatinine\":{\"value\":\"0.80\",\"flag\":\"NORMAL\"}}", DateTime.UtcNow);

            var (values, _) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, hospitalId, patientId, CancellationToken.None);

            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Creatinine"), Is.EqualTo(0.80m));
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_NonNumericResult_IsSkippedNotThrown()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-3";
            SeedApprovedReportWithResult(hospitalId, patientId,
                "{\"Urine Pregnancy Test\":{\"value\":\"Negative\",\"flag\":\"NORMAL\"},\"Serum Creatinine\":{\"value\":\"0.9\",\"flag\":\"NORMAL\"}}",
                DateTime.UtcNow);

            var (values, _) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, hospitalId, patientId, CancellationToken.None);

            Assert.That(values.ContainsKey("Urine Pregnancy Test"), Is.False);
            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Creatinine"), Is.EqualTo(0.9m));
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_NoApprovedReport_ReturnsEmpty()
        {
            var (values, approvedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, Guid.NewGuid(), "PTID-NONE", CancellationToken.None);

            Assert.That(values, Is.Empty);
            Assert.That(approvedAt, Is.Null);
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_NullPatientId_ReturnsEmptyWithoutThrowing()
        {
            var (values, approvedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, Guid.NewGuid(), null, CancellationToken.None);

            Assert.That(values, Is.Empty);
            Assert.That(approvedAt, Is.Null);
        }
    }
}
