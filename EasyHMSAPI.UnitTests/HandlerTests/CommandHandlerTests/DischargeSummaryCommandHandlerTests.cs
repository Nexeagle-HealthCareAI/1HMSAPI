using System;
using System.Collections.Generic;
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
    public class DischargeSummaryCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private DischargeSummaryCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DischargeSummaryCommandHandlers(_context);
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
        public async Task Handle_SavesMedications_ViaDeleteAndReinsert()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var firstSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                FinalDiagnosis = "Test",
                Medications = new List<DischargeMedicationModel>
                {
                    new() { MedicineName = "Paracetamol", Dosage = "500mg", Frequency = "BD", DisplayOrder = 0 },
                    new() { MedicineName = "Amoxicillin", Dosage = "250mg", Frequency = "TDS", DisplayOrder = 1 },
                },
            }, CancellationToken.None);

            Assert.That(firstSave.Success, Is.True);
            var afterFirst = _context.DischargeMedication.Where(m => m.DischargeSummaryId == firstSave.DischargeSummaryId).ToList();
            Assert.That(afterFirst, Has.Count.EqualTo(2));

            var secondSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                FinalDiagnosis = "Test",
                Medications = new List<DischargeMedicationModel>
                {
                    new() { MedicineName = "Ibuprofen", Dosage = "400mg", Frequency = "OD", DisplayOrder = 0 },
                },
            }, CancellationToken.None);

            Assert.That(secondSave.Success, Is.True);
            var afterSecond = _context.DischargeMedication.Where(m => m.DischargeSummaryId == secondSave.DischargeSummaryId).ToList();
            Assert.That(afterSecond, Has.Count.EqualTo(1));
            Assert.That(afterSecond[0].MedicineName, Is.EqualTo("Ibuprofen"));
        }

        [Test]
        public async Task Handle_EmptyMedicationsList_ClearsExistingRows()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var firstSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                Medications = new List<DischargeMedicationModel> { new() { MedicineName = "Paracetamol" } },
            }, CancellationToken.None);

            Assert.That(_context.DischargeMedication.Count(m => m.DischargeSummaryId == firstSave.DischargeSummaryId), Is.EqualTo(1));

            var secondSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                Medications = new List<DischargeMedicationModel>(),
            }, CancellationToken.None);

            Assert.That(secondSave.Success, Is.True);
            Assert.That(_context.DischargeMedication.Count(m => m.DischargeSummaryId == secondSave.DischargeSummaryId), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_DerivesLegacyTextColumn_FromStructuredMedications()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var response = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                Medications = new List<DischargeMedicationModel>
                {
                    new() { MedicineName = "Paracetamol", Dosage = "500mg", Route = "Oral", Frequency = "BD", Durations = "5d", Instructions = "After food" },
                },
            }, CancellationToken.None);

            var saved = _context.DischargeSummary.First(d => d.DischargeSummaryId == response.DischargeSummaryId);
            Assert.That(saved.DischargeMedications, Does.Contain("Paracetamol"));
            Assert.That(saved.DischargeMedications, Does.Contain("500mg"));
            Assert.That(saved.DischargeMedications, Does.Contain("After food"));
        }

        [Test]
        public async Task Handle_NullMedications_PreservesLegacyTextAndLeavesRowsUntouched()
        {
            var hospitalId = Guid.NewGuid();
            var admission = SeedAdmission(hospitalId);

            var firstSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                Medications = new List<DischargeMedicationModel> { new() { MedicineName = "Paracetamol" } },
            }, CancellationToken.None);

            var secondSave = await _handler.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = hospitalId,
                AdmissionId = admission.AdmissionId,
                DischargeMedications = "hand typed note",
                Medications = null,
            }, CancellationToken.None);

            Assert.That(secondSave.Success, Is.True);
            var saved = _context.DischargeSummary.First(d => d.DischargeSummaryId == secondSave.DischargeSummaryId);
            Assert.That(saved.DischargeMedications, Is.EqualTo("hand typed note"));
            Assert.That(_context.DischargeMedication.Count(m => m.DischargeSummaryId == firstSave.DischargeSummaryId), Is.EqualTo(1));
        }
    }
}
