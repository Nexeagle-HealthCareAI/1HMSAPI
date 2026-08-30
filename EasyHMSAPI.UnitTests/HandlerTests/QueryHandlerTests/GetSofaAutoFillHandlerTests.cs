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
    public class GetSofaAutoFillHandlerTests
    {
        private AppDbContext _context = null!;
        private GetSofaAutoFillHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetSofaAutoFillHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Admission SeedAdmission(Guid hospitalId, string patientId)
        {
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                PayerType = "CASH",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return admission;
        }

        [Test]
        public async Task Handle_PatientHasApprovedReport_PrefillsPlateletsBilirubinCreatinine()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-SOFA-1";
            var admission = SeedAdmission(hospitalId, patientId);

            var orderId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder { OrderId = orderId, HospitalId = hospitalId, PatientId = patientId, OrderNo = "O-1", Status = "COMPLETED" });
            var approvedAt = DateTime.UtcNow;
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = hospitalId, OrderId = orderId, ReportNo = "R-1", Status = "APPROVED", ApprovedAt = approvedAt });
            _context.PathologyOrderLine.Add(new PathologyOrderLine { OrderLineId = lineId, HospitalId = hospitalId, OrderId = orderId, TestId = Guid.NewGuid(), Status = "REPORT_APPROVED", ReportId = reportId });
            _context.PathologyResult.Add(new PathologyResult
            {
                ResultId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderLineId = lineId,
                ReportId = reportId,
                ResultValuesJson = "{\"Platelet Count\":{\"value\":\"180000\",\"flag\":\"NORMAL\"}," +
                    "\"Bilirubin - Total\":{\"value\":\"1.4\",\"flag\":\"HIGH\"}," +
                    "\"Serum Creatinine\":{\"value\":\"1.6\",\"flag\":\"HIGH\"}}",
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new GetSofaAutoFillRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.PlateletsCount, Is.EqualTo(180000m));
            Assert.That(response.BilirubinMgDl, Is.EqualTo(1.4m));
            Assert.That(response.CreatinineMgDl, Is.EqualTo(1.6m));
            Assert.That(response.SourceLabReportApprovedAt, Is.EqualTo(approvedAt));
        }

        [Test]
        public async Task Handle_PatientHasNoApprovedReport_LabFieldsStayNull()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId, "PTID-SOFA-2");

            var response = await _handler.Handle(new GetSofaAutoFillRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.PlateletsCount, Is.Null);
            Assert.That(response.BilirubinMgDl, Is.Null);
            Assert.That(response.CreatinineMgDl, Is.Null);
        }
    }
}
