using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DischargeAiHandlersTests
    {
        private AppDbContext _context = null!;
        private Mock<IClinicalAiClient> _ai = null!;
        private Mock<IVoiceRxService> _voice = null!;
        private Mock<ITranslationService> _translation = null!;
        private DischargeAiHandlers _handler = null!;
        private readonly Guid _hospitalId = Guid.NewGuid();
        private Admission _admission = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _ai = new Mock<IClinicalAiClient>();
            _ai.SetupGet(a => a.IsConfigured).Returns(true);
            _ai.SetupGet(a => a.NotConfiguredMessage).Returns("AI is not configured on this server (missing Groq API key).");
            _voice = new Mock<IVoiceRxService>();
            _translation = new Mock<ITranslationService>();
            _handler = new DischargeAiHandlers(_context, _ai.Object, _voice.Object, _translation.Object, NullLogger<DischargeAiHandlers>.Instance);

            _admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT21480165",
                AdmissionNo = "ADM-2026-0481",
                AdmittedAt = DateTime.UtcNow.AddDays(-3),
                Diagnosis = "Inferior wall MI",
                AdmissionReason = "Chest pain for 3 hours",
            };
            _context.Admission.Add(_admission);
            var patient = TestEntityFactory.CreatePatientRegistration(_hospitalId, "PT21480165", "Rajesh Kumar Sharma");
            patient.EmergencyContactName = "Amit Sharma";
            _context.PatientRegistrations.Add(patient);
            _context.RoundNote.Add(new RoundNote
            {
                RoundNoteId = Guid.NewGuid(), HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId,
                NotedAt = DateTime.UtcNow.AddDays(-1), Subjective = "Rajesh says no chest pain; son Amit (9876543210) present",
                Assessment = "Stable", Plan = "Plan discharge",
            });
            AddMedication("Inj. Ceftriaxone 1 g", IpdConstants.ClinicalOrderLineStatus.Active);
            AddMedication("Tab. Metoprolol 25 mg", IpdConstants.ClinicalOrderLineStatus.Active);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void AddMedication(string name, string status)
        {
            var order = new ClinicalOrder
            {
                OrderId = Guid.NewGuid(), HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId,
                PatientId = _admission.PatientId, OrderType = IpdConstants.ClinicalOrderType.Medication, OrderedAt = DateTime.UtcNow,
            };
            _context.ClinicalOrder.Add(order);
            _context.ClinicalOrderLine.Add(new ClinicalOrderLine { OrderLineId = Guid.NewGuid(), OrderId = order.OrderId, HospitalId = _hospitalId, ItemName = name, StatusCode = status });
        }

        private void SetHospitalAi(bool enabled)
        {
            var row = TestEntityFactory.CreateDischargeSetting(_hospitalId, Guid.Empty);
            row.AiAssistEnabled = enabled;
            _context.DischargeSettings.Add(row);
            _context.SaveChanges();
        }

        [Test]
        public async Task Draft_SendsDeIdentifiedRecords_AndReturnsOnlyRequestedSections()
        {
            string? sent = null;
            _ai.Setup(a => a.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, user, _) => sent = user)
                .ReturnsAsync("{\"courseInHospital\":\"Stable throughout.\",\"warningSigns\":\"Chest pain\",\"dischargeMedications\":\"Aspirin\"}");

            var res = await _handler.Handle(new DraftDischargeSectionsRequestModel
            {
                HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Sections = new() { "courseInHospital", "warningSigns" },
            }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Sections.Keys, Is.EquivalentTo(new[] { "courseInHospital", "warningSigns" }));
            Assert.That(res.SourceSummary, Does.Contain("1 round note"));
            Assert.That(sent, Is.Not.Null);
            foreach (var leaked in new[] { "Rajesh", "Sharma", "Amit", "9876543210", "PT21480165", "ADM-2026-0481" })
                Assert.That(sent, Does.Not.Contain(leaked), leaked);
            Assert.That(sent, Does.Contain("Inferior wall MI"));
            Assert.That(sent, Does.Contain("30-year-old male"));
        }

        [Test]
        public async Task Draft_WhenHospitalSwitchedAiOff_DoesNotCallAi()
        {
            SetHospitalAi(false);
            var res = await _handler.Handle(new DraftDischargeSectionsRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
            Assert.That(res.Message, Is.EqualTo(DischargeAiHandlers.AiOffMessage));
            _ai.Verify(a => a.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Draft_WhenSummarySigned_DoesNotCallAi()
        {
            _context.DischargeSummary.Add(new DischargeSummary { DischargeSummaryId = Guid.NewGuid(), HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, IsSigned = true });
            _context.SaveChanges();
            var res = await _handler.Handle(new DraftDischargeSectionsRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
            Assert.That(res.Message, Is.EqualTo(DischargeAiHandlers.SignedMessage));
            _ai.Verify(a => a.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Draft_WhenServerHasNoKey_ReportsIt()
        {
            _ai.SetupGet(a => a.IsConfigured).Returns(false);
            var res = await _handler.Handle(new DraftDischargeSectionsRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
            Assert.That(res.Message, Does.Contain("Groq"));
        }

        [Test]
        public async Task Dictate_KeepsChangesForListedMedicines_AndAdds_ButDropsUnknownStops()
        {
            _voice.Setup(v => v.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync("Stop ceftriaxone, stop warfarin, add pantoprazole forty once daily, follow up in one week");
            _ai.Setup(a => a.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("{\"followUpInstructions\":\"Review in OPD after one week.\",\"followUpAfterDays\":7,\"medicationChanges\":[" +
                              "{\"medicineName\":\"Ceftriaxone\",\"action\":\"STOP\"},{\"medicineName\":\"Warfarin\",\"action\":\"STOP\"}," +
                              "{\"medicineName\":\"Pantoprazole\",\"action\":\"ADD\",\"newDose\":\"40 mg once daily\"}]}");

            var audio = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "audio", "d.webm");
            var res = await _handler.Handle(new DictateDischargeRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Audio = audio }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.FollowUpAfterDays, Is.EqualTo(7));
            Assert.That(res.Sections["followUpInstructions"], Does.Contain("one week"));
            Assert.That(res.MedicationChanges.Select(c => c.MedicineName), Is.EqualTo(new[] { "Ceftriaxone", "Pantoprazole" }));
            Assert.That(res.MedicationChanges[0].MatchesMedicine, Is.EqualTo("Inj. Ceftriaxone 1 g"));
        }

        [Test]
        public async Task Dictate_WithoutAudio_Fails()
        {
            var res = await _handler.Handle(new DictateDischargeRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task Rewrite_RejectsSectionsAiMayNotWrite()
        {
            var res = await _handler.Handle(new RewriteDischargeSectionRequestModel
            {
                HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Section = "dischargeMedications", Style = "SHORTER", Text = "Aspirin",
            }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
            _ai.Verify(a => a.CompleteTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Rewrite_ReturnsCleanText_AndRedactsName()
        {
            string? sent = null;
            _ai.Setup(a => a.CompleteTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, user, _) => sent = user)
                .ReturnsAsync("**Stable**, discharged.");
            var res = await _handler.Handle(new RewriteDischargeSectionRequestModel
            {
                HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Section = "courseInHospital", Style = "shorter", Text = "Mr Rajesh Sharma remained stable and was discharged.",
            }, CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Text, Is.EqualTo("Stable, discharged."));
            Assert.That(sent, Does.Not.Contain("Rajesh"));
        }

        [Test]
        public async Task Translate_OnlyPatientAdvice_IntoHindi()
        {
            Dictionary<string, string>? sent = null;
            _translation.Setup(t => t.TranslateMultipleAsync(It.IsAny<Dictionary<string, string>>(), "Hindi"))
                .Callback<Dictionary<string, string>, string>((d, _) => sent = d)
                .ReturnsAsync(new Dictionary<string, string> { ["dietInstructions"] = "कम नमक" });
            var res = await _handler.Handle(new TranslateDischargeAdviceRequestModel
            {
                HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Language = "hi",
                Sections = new() { ["dietInstructions"] = "Low salt", ["courseInHospital"] = "Stable" },
            }, CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(sent!.Keys, Is.EquivalentTo(new[] { "dietInstructions" }));
            Assert.That(res.Sections["dietInstructions"], Is.EqualTo("कम नमक"));
        }

        [Test]
        public async Task Translate_UnknownLanguage_Fails()
        {
            var res = await _handler.Handle(new TranslateDischargeAdviceRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, Language = "xx" }, CancellationToken.None);
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task HospitalPreferences_DefaultToEnglishAndAiOn_ThenUpdate()
        {
            var prefs = new HospitalDischargePreferencesHandlers(_context, _ai.Object);
            var before = await prefs.Handle(new GetHospitalDischargePreferencesRequestModel { HospitalId = _hospitalId }, CancellationToken.None);
            Assert.That(before.PatientLanguage, Is.Null);
            Assert.That(before.AiAssistEnabled, Is.True);
            Assert.That(before.AiAvailable, Is.True);
            Assert.That(before.LanguageOptions.Select(l => l.Code), Does.Contain("hi"));

            SetHospitalAi(true); // InMemory can't create rows with a [Timestamp] column, so seed the hospital row
            var saved = await prefs.Handle(new UpdateHospitalDischargePreferencesRequestModel { HospitalId = _hospitalId, PatientLanguage = "hi", AiAssistEnabled = false }, CancellationToken.None);
            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(saved.PatientLanguage, Is.EqualTo("hi"));
            Assert.That(saved.AiAssistEnabled, Is.False);

            var bad = await prefs.Handle(new UpdateHospitalDischargePreferencesRequestModel { HospitalId = _hospitalId, PatientLanguage = "fr" }, CancellationToken.None);
            Assert.That(bad.Success, Is.False);
        }

        [Test]
        public async Task FreshDraft_PutsDiagnosisAndComplaintTheRightWayRound_AndMarksSources()
        {
            var res = await new GetDischargeSummaryDraftHandler(_context).Handle(
                new GetDischargeSummaryDraftRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId }, CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Draft!.AdmittingDiagnosis, Is.EqualTo("Inferior wall MI"));
            Assert.That(res.Draft.ChiefComplaint, Is.EqualTo("Chest pain for 3 hours"));
            Assert.That(res.Draft.SectionSourcesJson, Does.Contain("\"courseInHospital\":\"RECORDS\""));
            Assert.That(res.Draft.Medications, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Save_KeepsNewSections_WhenAnOlderClientDoesNotSendThem()
        {
            var save = new DischargeSummaryCommandHandlers(_context);
            var first = await save.Handle(new SaveDischargeSummaryRequestModel
            {
                HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId,
                KeyInvestigations = "- CBC: Hb 8.2", WarningSigns = "Chest pain",
                PatientAdviceTranslationsJson = "{\"hi\":{\"warningSigns\":\"सीने में दर्द\"}}",
                SectionSourcesJson = "{\"warningSigns\":\"ai\",\"junk\":\"X\"}",
            }, CancellationToken.None);
            Assert.That(first.Success, Is.True, first.Message);

            var second = await save.Handle(new SaveDischargeSummaryRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, CourseInHospital = "Stable" }, CancellationToken.None);
            Assert.That(second.Success, Is.True, second.Message);

            var row = _context.DischargeSummary.Single(d => d.AdmissionId == _admission.AdmissionId);
            Assert.That(row.KeyInvestigations, Is.EqualTo("- CBC: Hb 8.2"));
            Assert.That(row.WarningSigns, Is.EqualTo("Chest pain"));
            Assert.That(row.PatientAdviceTranslationsJson, Does.Contain("hi"));
            Assert.That(row.SectionSourcesJson, Is.EqualTo("{\"warningSigns\":\"AI\"}"));

            await save.Handle(new SaveDischargeSummaryRequestModel { HospitalId = _hospitalId, AdmissionId = _admission.AdmissionId, WarningSigns = "" }, CancellationToken.None);
            Assert.That(_context.DischargeSummary.Single(d => d.AdmissionId == _admission.AdmissionId).WarningSigns, Is.Null);
        }
    }

    [TestFixture]
    public class HospitalLetterheadFallbackTests
    {
        [Test]
        public async Task DoctorWithoutLetterhead_GetsHospitalLetterheadAsDefault()
        {
            using var context = InMemoryDbContextFactory.CreateContext();
            var hospitalId = Guid.NewGuid();
            var doctorId = Guid.NewGuid();
            context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId));
            context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId));
            var hospitalRow = TestEntityFactory.CreateDischargeSetting(hospitalId, Guid.Empty);
            hospitalRow.URI = "https://files/hospital.pdf";
            context.DischargeSettings.Add(hospitalRow);
            context.SaveChanges();

            var validation = new Mock<IDoctorValidationHelper>();
            validation.Setup(v => v.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var blob = new Mock<IBlobStorageService>();
            blob.Setup(b => b.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string _, string? url, CancellationToken _) => url);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var handler = new GetDischargeSettingsHandler(context, validation.Object, blob.Object, config);

            var forDoctor = await handler.Handle(new GetDischargeSettingsRequestModel { DoctorId = doctorId, HospitalId = hospitalId }, CancellationToken.None);
            Assert.That(forDoctor.Success, Is.True, forDoctor.Message);
            Assert.That(forDoctor.Data, Is.Null);
            Assert.That(forDoctor.HospitalDefault!.URI, Is.EqualTo("https://files/hospital.pdf"));
            Assert.That(forDoctor.HospitalDefault.IsHospitalLetterhead, Is.True);

            var forHospital = await handler.Handle(new GetDischargeSettingsRequestModel { DoctorId = Guid.Empty, HospitalId = hospitalId }, CancellationToken.None);
            Assert.That(forHospital.Data!.URI, Is.EqualTo("https://files/hospital.pdf"));
            Assert.That(forHospital.HospitalDefault, Is.Null);
            validation.Verify(v => v.ValidateDoctorAsync(hospitalId, Guid.Empty, It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
