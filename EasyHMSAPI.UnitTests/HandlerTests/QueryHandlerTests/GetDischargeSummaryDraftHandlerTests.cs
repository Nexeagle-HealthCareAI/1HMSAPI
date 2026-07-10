using System;
using System.Linq;
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
    public class GetDischargeSummaryDraftHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDischargeSummaryDraftHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDischargeSummaryDraftHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Admission SeedAdmission(Guid hospitalId, string patientId = "PAT123")
        {
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return admission;
        }

        [Test]
        public async Task Handle_ComposesStructuredMedicationRows_FromActiveClinicalOrders_WhenNoSummaryExists()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var order = new ClinicalOrder
            {
                OrderId = Guid.NewGuid(),
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                PatientId = admission.PatientId,
                OrderType = IpdConstants.ClinicalOrderType.Medication,
                OrderedAt = DateTime.UtcNow,
            };
            _context.ClinicalOrder.Add(order);
            _context.ClinicalOrderLine.Add(new ClinicalOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                OrderId = order.OrderId,
                HospitalId = hospitalId,
                ItemName = "Paracetamol",
                Dose = "500mg",
                Route = "Oral",
                Frequency = "BD",
                DurationDays = 5,
                Instructions = "After food",
                StatusCode = IpdConstants.ClinicalOrderLineStatus.Active,
            });
            // Discontinued line should be excluded from the compose.
            _context.ClinicalOrderLine.Add(new ClinicalOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                OrderId = order.OrderId,
                HospitalId = hospitalId,
                ItemName = "Old Drug",
                StatusCode = IpdConstants.ClinicalOrderLineStatus.Discontinued,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDischargeSummaryDraftRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Draft!.Medications, Has.Count.EqualTo(1));
            Assert.That(response.Draft.Medications[0].MedicineName, Is.EqualTo("Paracetamol"));
            Assert.That(response.Draft.Medications[0].Dosage, Is.EqualTo("500mg"));
            Assert.That(response.Draft.Medications[0].Durations, Is.EqualTo("5d"));
        }

        [Test]
        public async Task Handle_ReturnsSavedMedicationRows_WhenSummaryExists()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var summary = new DischargeSummary
            {
                DischargeSummaryId = Guid.NewGuid(),
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                PatientId = admission.PatientId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.DischargeSummary.Add(summary);
            _context.DischargeMedication.Add(new DischargeMedication
            {
                DischargeMedicationId = Guid.NewGuid(),
                DischargeSummaryId = summary.DischargeSummaryId,
                MedicineName = "Ibuprofen",
                Dosage = "400mg",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDischargeSummaryDraftRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Draft!.DischargeSummaryId, Is.EqualTo(summary.DischargeSummaryId));
            Assert.That(response.Draft.Medications, Has.Count.EqualTo(1));
            Assert.That(response.Draft.Medications[0].MedicineName, Is.EqualTo("Ibuprofen"));
        }
    }
}
