using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Services;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class AiProviderConfigTests
    {
        private static IConfiguration Config(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        [Test]
        public void Groq_IsUsed_WhenGroqKeySet_EvenIfOpenAiKeyAlsoSet()
        {
            var c = AiProviderConfig.Resolve(Config(new() { ["Groq:ApiKey"] = "gsk_real", ["Groq:Model"] = "openai/gpt-oss-120b", ["OpenAI:ApiKey"] = "sk-x" }));
            Assert.That(c.Provider, Is.EqualTo("Groq"));
            Assert.That(c.ApiKey, Is.EqualTo("gsk_real"));
            Assert.That(c.BaseUrl, Is.EqualTo("https://api.groq.com/openai/v1"));
            Assert.That(c.ChatModel, Is.EqualTo("openai/gpt-oss-120b"));
            Assert.That(c.TranscriptionModel, Is.EqualTo("whisper-large-v3-turbo"));
        }

        [Test]
        public void PlaceholderGroqKey_FallsBackToOpenAi()
        {
            var c = AiProviderConfig.Resolve(Config(new() { ["Groq:ApiKey"] = "<set-via-env-Groq-ApiKey>", ["OpenAI:ApiKey"] = "sk-x" }));
            Assert.That(c.Provider, Is.EqualTo("OpenAI"));
            Assert.That(c.ChatModel, Is.EqualTo("gpt-4o-mini"));
            Assert.That(c.TranscriptionModel, Is.EqualTo("whisper-1"));
        }

        [Test]
        public void NoKeys_IsNotConfigured_AndNamesGroq()
        {
            var c = AiProviderConfig.Resolve(Config(new() { ["OpenAI:ApiKey"] = "<set-via-env-OpenAI-ApiKey>" }));
            Assert.That(c.IsConfigured, Is.False);
            Assert.That(c.NotConfiguredMessage, Does.Contain("Groq"));
        }

        [Test]
        public void ClinicalModel_And_TranscriptionModel_CanBeOverridden()
        {
            var c = AiProviderConfig.Resolve(Config(new() { ["Groq:ApiKey"] = "gsk", ["Groq:Model"] = "a", ["Groq:ClinicalModel"] = "b", ["Groq:TranscriptionModel"] = "whisper-large-v3", ["Groq:BaseUrl"] = "https://proxy/v1/" }));
            Assert.That(c.ChatModel, Is.EqualTo("b"));
            Assert.That(c.TranscriptionModel, Is.EqualTo("whisper-large-v3"));
            Assert.That(c.BaseUrl, Is.EqualTo("https://proxy/v1"));
        }
    }

    [TestFixture]
    public class DischargeAiRulesTests
    {
        [Test]
        public void Redact_RemovesNames_Ids_Phones_Aadhaar_Email()
        {
            var text = "Rajesh Kumar Sharma (PT21480165, ADM-2026-0481) seen. Son Amit Sharma on 9876543210 / +91 98765 43210? " +
                       "Aadhaar 1234 5678 9012, ABHA rajesh.s@sbx, mail rk@example.com. Mr Rajesh improving.";
            var r = DischargeAiRules.Redact(text, new[] { "Rajesh Kumar Sharma" }, new[] { "Amit Sharma" }, new[] { "PT21480165", "ADM-2026-0481" });

            foreach (var leaked in new[] { "Rajesh", "Sharma", "Amit", "PT21480165", "ADM-2026-0481", "9876543210", "98765 43210", "1234 5678 9012", "rajesh.s@sbx", "rk@example.com" })
                Assert.That(r, Does.Not.Contain(leaked), leaked);
            Assert.That(r, Does.Contain("the patient"));
            Assert.That(r, Does.Contain("improving"));
        }

        [Test]
        public void Redact_KeepsClinicalNumbers_AndCommonNameWords()
        {
            var r = DischargeAiRules.Redact("Hb 8.2, BP 132/84, troponin 0.8. Sunita Devi stable.", new[] { "Sunita Devi" });
            Assert.That(r, Does.Contain("Hb 8.2"));
            Assert.That(r, Does.Contain("132/84"));
            Assert.That(r, Does.Not.Contain("Sunita"));
        }

        [Test]
        public void ResolveSections_EmptyMeansAll_AndUnknownKeysAreDropped()
        {
            Assert.That(DischargeAiRules.ResolveSections(null), Has.Count.EqualTo(DischargeAiRules.DraftableSections.Count));
            var some = DischargeAiRules.ResolveSections(new[] { "courseInHospital", "dischargeMedications", "finalDiagnosis", "WARNINGSIGNS" });
            Assert.That(some, Is.EquivalentTo(new[] { "courseInHospital", "warningSigns" }));
        }

        [Test]
        public void AiNeverDrafts_MedicinesDiagnosesOrDates()
        {
            foreach (var k in new[] { "dischargeMedications", "medications", "finalDiagnosis", "admittingDiagnosis", "followUpDate", "conditionAtDischarge" })
                Assert.That(DischargeAiRules.DraftableSections.ContainsKey(k), Is.False, k);
        }

        [Test]
        public void ParseSections_KeepsAllowedKeys_CleansMarkdown_AndSkipsEmpty()
        {
            var json = "Here you go:\n```json\n{\"courseInHospital\":\"**Stable** throughout.\",\"dietInstructions\":\"\",\"finalDiagnosis\":\"MI\",\"warningSigns\":[\"Chest pain\",\"• Breathlessness\"]}\n```";
            var s = DischargeAiRules.ParseSections(json, new[] { "courseInHospital", "dietInstructions", "warningSigns" });
            Assert.That(s.Keys, Is.EquivalentTo(new[] { "courseInHospital", "warningSigns" }));
            Assert.That(s["courseInHospital"], Is.EqualTo("Stable throughout."));
            Assert.That(s["warningSigns"], Is.EqualTo("- Chest pain\n- Breathlessness"));
        }

        [Test]
        public void ParseSections_Garbage_ReturnsEmpty()
        {
            Assert.That(DischargeAiRules.ParseSections("not json", new[] { "courseInHospital" }), Is.Empty);
            Assert.That(DischargeAiRules.ParseSections(null, new[] { "courseInHospital" }), Is.Empty);
        }

        [Test]
        public void ParseMedicationChanges_ValidatesAction()
        {
            var json = "{\"medicationChanges\":[{\"medicineName\":\"Ceftriaxone\",\"action\":\"stop\"},{\"medicineName\":\"X\",\"action\":\"DOUBLE\"},{\"medicineName\":\"Pantoprazole 40\",\"action\":\"ADD\",\"newDose\":\"40 mg OD\"}]}";
            var c = DischargeAiRules.ParseMedicationChanges(json);
            Assert.That(c.Select(x => x.Action), Is.EqualTo(new[] { "STOP", "ADD" }));
            Assert.That(c[1].NewDose, Is.EqualTo("40 mg OD"));
        }

        [TestCase("{\"followUpAfterDays\":7}", 7)]
        [TestCase("{\"followUpAfterDays\":\"14\"}", 14)]
        [TestCase("{\"followUpAfterDays\":null}", null)]
        [TestCase("{\"followUpAfterDays\":900}", null)]
        public void ParseFollowUpDays(string json, int? expected) =>
            Assert.That(DischargeAiRules.ParseFollowUpDays(json), Is.EqualTo(expected));

        [Test]
        public void MatchMedicine_OnCoreDrugWord()
        {
            var list = new[] { "Tab. Metoprolol 25 mg", "Inj. Ceftriaxone 1 g", "Tab. Aspirin 75 mg" };
            Assert.That(DischargeAiRules.MatchMedicine("ceftriaxone", list), Is.EqualTo("Inj. Ceftriaxone 1 g"));
            Assert.That(DischargeAiRules.MatchMedicine("Metoprolol 50", list), Is.EqualTo("Tab. Metoprolol 25 mg"));
            Assert.That(DischargeAiRules.MatchMedicine("Pantoprazole", list), Is.Null);
            Assert.That(DischargeAiRules.MatchMedicine("Ab", list), Is.Null);
        }

        [Test]
        public void NormalizeSectionSources_DropsUnknownValues()
        {
            var s = DischargeAiRules.NormalizeSectionSources("{\"courseInHospital\":\"ai\",\"x\":\"MAGIC\",\"y\":3}");
            Assert.That(s, Is.EqualTo("{\"courseInHospital\":\"AI\"}"));
            Assert.That(DischargeAiRules.NormalizeSectionSources("nope"), Is.Null);
        }

        [Test]
        public void Language_And_Style_AreWhitelisted()
        {
            Assert.That(DischargeAiRules.NormalizeLanguage("HI"), Is.EqualTo("hi"));
            Assert.That(DischargeAiRules.NormalizeLanguage("fr"), Is.Null);
            Assert.That(DischargeAiRules.NormalizeStyle("shorter"), Is.EqualTo("SHORTER"));
            Assert.That(DischargeAiRules.NormalizeStyle("poem"), Is.Null);
        }

        [Test]
        public void DraftPrompt_ListsOnlyRequestedKeys_AndForbidsInvention()
        {
            var p = DischargeAiRules.BuildDraftSystemPrompt(new[] { "courseInHospital", "warningSigns" });
            Assert.That(p, Does.Contain("\"courseInHospital\""));
            Assert.That(p, Does.Contain("\"warningSigns\""));
            Assert.That(p, Does.Not.Contain("\"dietInstructions\""));
            Assert.That(p, Does.Contain("Never invent"));
        }
    }

    [TestFixture]
    public class DischargeSummaryComposerInvestigationTests
    {
        [Test]
        public void FormatResultValues_HandlesFlaggedAndLegacyShapes()
        {
            Assert.That(DischargeSummaryComposer.FormatResultValues("{\"Hemoglobin\":{\"value\":\"8.2\",\"flag\":\"LOW\"},\"WBC\":{\"value\":\"9000\",\"flag\":\"NORMAL\"}}"),
                Is.EqualTo("Hemoglobin 8.2 (LOW), WBC 9000"));
            Assert.That(DischargeSummaryComposer.FormatResultValues("{\"Sodium\":\"136\",\"K\":4.1}"), Is.EqualTo("Sodium 136, K 4.1"));
            Assert.That(DischargeSummaryComposer.FormatResultValues("[]"), Is.Null);
            Assert.That(DischargeSummaryComposer.FormatResultValues("{bad"), Is.Null);
        }

        [Test]
        public void ComposeKeyInvestigations_ListsResultedTestsOnly()
        {
            var labs = new List<DischargeSummaryComposer.LabResultLine>
            {
                new("CBC", new DateTime(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc), "{\"Hemoglobin\":{\"value\":\"8.2\",\"flag\":\"LOW\"}}", null, false),
                new("Troponin I", new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc), "{\"Troponin I\":{\"value\":\"2.1\",\"flag\":\"CRITICAL_HIGH\"}}", "Raised", true),
                new("Lipid profile", null, null, null, false),
            };
            var text = DischargeSummaryComposer.ComposeKeyInvestigations(labs);
            Assert.That(text, Is.EqualTo("- Troponin I (22 Nov): Troponin I 2.1 (CRITICAL HIGH) — Raised [critical value]\n- CBC (23 Nov): Hemoglobin 8.2 (LOW)"));
            Assert.That(DischargeSummaryComposer.ComposeKeyInvestigations(new()), Is.Null);
        }
    }
}
