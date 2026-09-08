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
    public class GetApacheIIAutoFillHandlerTests
    {
        private AppDbContext _context = null!;
        private GetApacheIIAutoFillHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetApacheIIAutoFillHandler(_context);
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
        public async Task Handle_PatientHasApprovedReport_PrefillsElectrolytesCreatinineHematocritWbc()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-APACHE-1";
            var admission = SeedAdmission(hospitalId, patientId);

            var orderId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder { OrderId = orderId, HospitalId = hospitalId, PatientId = patientId, OrderNo = "O-1", Status = "COMPLETED" });
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = hospitalId, OrderId = orderId, ReportNo = "R-1", Status = "GENERATED", GeneratedAt = DateTime.UtcNow });
            _context.PathologyOrderLine.Add(new PathologyOrderLine { OrderLineId = lineId, HospitalId = hospitalId, OrderId = orderId, TestId = Guid.NewGuid(), Status = "RESULT_ENTERED", ReportId = reportId });
            _context.PathologyResult.Add(new PathologyResult
            {
                ResultId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderLineId = lineId,
                ReportId = reportId,
                ResultValuesJson = "{\"Serum Sodium (Na+)\":{\"value\":\"133.0\",\"flag\":\"LOW\"}," +
                    "\"Serum Potassium (K+)\":{\"value\":\"5.8\",\"flag\":\"HIGH\"}," +
                    "\"Serum Creatinine\":{\"value\":\"2.1\",\"flag\":\"HIGH\"}," +
                    "\"PCV / Hematocrit\":{\"value\":\"29.0\",\"flag\":\"LOW\"}," +
                    "\"Total WBC Count (TLC)\":{\"value\":\"15200\",\"flag\":\"HIGH\"}}",
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new GetApacheIIAutoFillRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.SerumSodium, Is.EqualTo(133.0m));
            Assert.That(response.SerumPotassium, Is.EqualTo(5.8m));
            Assert.That(response.SerumCreatinine, Is.EqualTo(2.1m));
            Assert.That(response.Hematocrit, Is.EqualTo(29.0m));
            Assert.That(response.Wbc, Is.EqualTo(15200m));
            Assert.That(response.SourceLabReportApprovedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_PatientHasNoApprovedReport_LabFieldsStayNull()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId, "PTID-APACHE-2");

            var response = await _handler.Handle(new GetApacheIIAutoFillRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.SerumSodium, Is.Null);
            Assert.That(response.SerumPotassium, Is.Null);
            Assert.That(response.SerumCreatinine, Is.Null);
            Assert.That(response.Hematocrit, Is.Null);
            Assert.That(response.Wbc, Is.Null);
        }
    }
}
