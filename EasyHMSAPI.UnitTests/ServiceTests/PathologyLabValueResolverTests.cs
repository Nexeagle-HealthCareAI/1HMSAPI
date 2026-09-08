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
    // There's no separate "approved" milestone anymore (the technician/pathologist sign-off
    // workflow was removed), so selection is by GeneratedAt, not a Status filter.
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

        private PathologyReport SeedGeneratedReportWithResult(
            Guid hospitalId, string patientId, string resultValuesJson, DateTime generatedAt)
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
                Status = "GENERATED",
                GeneratedAt = generatedAt,
            };
            _context.PathologyReport.Add(report);
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = orderLineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "RESULT_ENTERED",
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
            var generatedAt = DateTime.UtcNow;
            SeedGeneratedReportWithResult(hospitalId, patientId,
                "{\"Serum Creatinine\":{\"value\":\"0.95\",\"flag\":\"NORMAL\"},\"Serum Sodium (Na+)\":{\"value\":\"138.0\",\"flag\":\"NORMAL\"}}",
                generatedAt);

            var (values, resultGeneratedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, hospitalId, patientId, CancellationToken.None);

            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Creatinine"), Is.EqualTo(0.95m));
            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Sodium (Na+)"), Is.EqualTo(138.0m));
            Assert.That(resultGeneratedAt, Is.EqualTo(generatedAt));
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_MultipleGeneratedReports_PicksMostRecent()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-2";
            SeedGeneratedReportWithResult(hospitalId, patientId,
                "{\"Serum Creatinine\":{\"value\":\"1.10\",\"flag\":\"NORMAL\"}}", DateTime.UtcNow.AddDays(-5));
            SeedGeneratedReportWithResult(hospitalId, patientId,
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
            SeedGeneratedReportWithResult(hospitalId, patientId,
                "{\"Urine Pregnancy Test\":{\"value\":\"Negative\",\"flag\":\"NORMAL\"},\"Serum Creatinine\":{\"value\":\"0.9\",\"flag\":\"NORMAL\"}}",
                DateTime.UtcNow);

            var (values, _) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, hospitalId, patientId, CancellationToken.None);

            Assert.That(values.ContainsKey("Urine Pregnancy Test"), Is.False);
            Assert.That(PathologyLabValueResolver.TryGet(values, "Serum Creatinine"), Is.EqualTo(0.9m));
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_NoGeneratedReport_ReturnsEmpty()
        {
            var (values, generatedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, Guid.NewGuid(), "PTID-NONE", CancellationToken.None);

            Assert.That(values, Is.Empty);
            Assert.That(generatedAt, Is.Null);
        }

        [Test]
        public async Task GetLatestApprovedValuesAsync_NullPatientId_ReturnsEmptyWithoutThrowing()
        {
            var (values, generatedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, Guid.NewGuid(), null, CancellationToken.None);

            Assert.That(values, Is.Empty);
            Assert.That(generatedAt, Is.Null);
        }
    }
}
