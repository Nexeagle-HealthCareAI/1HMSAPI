#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using EasyHMSAPI.Application.Services.Fhir;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 6: FHIR document bundles. These tests hold the structural rules every bundle
    // must meet (Composition first, every reference inside the bundle, nothing empty, no invented
    // codes). They do not replace the official HL7 validator run against the NRCES guide.
    [TestFixture]
    public class AbdmFhirTests
    {
        private static readonly FhirContext Context = new(
            new FhirPatient("P-0001", "Asha Rao", "Female", new DateTime(1990, 3, 5), "91-1234-5678-9012", "asha@sbx"),
            new FhirAuthor("doc-1", "Dr Meera Iyer", "MH-12345"),
            new FhirFacility("hosp-1", "City Hospital", "IN2810014366"),
            new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc),
            "OPD-abc");

        private static readonly FhirMedicine Paracetamol = new("Paracetamol 500 mg", "1 tablet", "three times a day", "5 days", "Oral", "after food");

        private static JsonArray Entries(JsonObject bundle) => (JsonArray)bundle["entry"]!;
        private static JsonObject ResourceAt(JsonObject bundle, int index) => (JsonObject)Entries(bundle)[index]!["resource"]!;
        private static List<JsonObject> Resources(JsonObject bundle, string type) =>
            Entries(bundle).Select(e => (JsonObject)e!["resource"]!).Where(r => r["resourceType"]!.GetValue<string>() == type).ToList();

        private static void AssertSound(JsonObject bundle, string compositionProfile)
        {
            Assert.That(bundle["resourceType"]!.GetValue<string>(), Is.EqualTo("Bundle"));
            Assert.That(bundle["type"]!.GetValue<string>(), Is.EqualTo("document"));
            Assert.That(bundle["meta"]!["profile"]![0]!.GetValue<string>(), Does.EndWith("/DocumentBundle"));
            Assert.That(bundle["identifier"]!["value"]!.GetValue<string>(), Is.Not.Empty);
            Assert.That(bundle["timestamp"]!.GetValue<string>(), Does.EndWith("Z"));

            var composition = ResourceAt(bundle, 0);
            Assert.That(composition["resourceType"]!.GetValue<string>(), Is.EqualTo("Composition"), "the Composition is the first entry");
            Assert.That(composition["meta"]!["profile"]![0]!.GetValue<string>(), Does.EndWith("/" + compositionProfile));
            Assert.That(composition["status"]!.GetValue<string>(), Is.EqualTo("final"));
            Assert.That(((JsonArray)composition["section"]!).Count, Is.GreaterThan(0));

            Assert.That(AbdmFhir.UnresolvedReferences(bundle), Is.Empty, "every reference points inside the bundle");
            Assert.That(AbdmFhir.HasContent(bundle), Is.True);
            var json = bundle.ToJsonString();
            Assert.That(json, Does.Not.Contain(":null").And.Not.Contain(":\"\"").And.Not.Contain(":[]"), "FHIR has no empty values");

            var fullUrls = Entries(bundle).Select(e => e!["fullUrl"]!.GetValue<string>()).ToList();
            Assert.That(fullUrls, Is.Unique);
        }

        [Test]
        public void Prescription_IsASoundDocument_WithTheMedicinesAsText()
        {
            var bundle = AbdmFhir.Prescription(Context, new[] { Paracetamol, new FhirMedicine(" ", null, null, null, null, null) }, "Viral fever");

            AssertSound(bundle, "PrescriptionRecord");
            var request = Resources(bundle, "MedicationRequest").Single();
            Assert.That(request["medicationCodeableConcept"]!["text"]!.GetValue<string>(), Is.EqualTo("Paracetamol 500 mg"));
            Assert.That(((JsonObject)request["medicationCodeableConcept"]!).ContainsKey("coding"), Is.False, "no drug code is invented");
            Assert.That(request["dosageInstruction"]![0]!["text"]!.GetValue<string>(), Is.EqualTo("1 tablet, three times a day, 5 days, after food"));
            Assert.That(Resources(bundle, "Condition").Single()["code"]!["text"]!.GetValue<string>(), Is.EqualTo("Viral fever"));
        }

        [Test]
        public void ThePatient_CarriesOurIdAndTheirAbha_AndNothingWeDoNotKnow()
        {
            var patient = Resources(AbdmFhir.Prescription(Context, new[] { Paracetamol }, null), "Patient").Single();

            var identifiers = ((JsonArray)patient["identifier"]!).Select(i => i!["value"]!.GetValue<string>()).ToList();
            Assert.That(identifiers, Is.EqualTo(new[] { "P-0001", "91-1234-5678-9012", "asha@sbx" }));
            Assert.That(patient["gender"]!.GetValue<string>(), Is.EqualTo("female"));
            Assert.That(patient["birthDate"]!.GetValue<string>(), Is.EqualTo("1990-03-05"));

            var bare = Context with { Patient = new FhirPatient("P-2", null, null, null, null, null) };
            var unknown = Resources(AbdmFhir.Prescription(bare, new[] { Paracetamol }, null), "Patient").Single();
            Assert.That(unknown["gender"]!.GetValue<string>(), Is.EqualTo("unknown"));
            Assert.That(unknown.ContainsKey("birthDate"), Is.False);
            Assert.That(unknown["name"]![0]!["text"]!.GetValue<string>(), Is.EqualTo("P-2"));
        }

        [Test]
        public void DiagnosticReport_NamesTheTests_AndKeepsTheConclusion()
        {
            var bundle = AbdmFhir.DiagnosticReport(Context, "LAB-0042", new[] { "Complete Blood Count", "CRP", "CRP" }, "Mild leucocytosis.");

            AssertSound(bundle, "DiagnosticReportRecord");
            var report = Resources(bundle, "DiagnosticReport").Single();
            Assert.That(report["code"]!["text"]!.GetValue<string>(), Is.EqualTo("Complete Blood Count, CRP"));
            Assert.That(report["conclusion"]!.GetValue<string>(), Is.EqualTo("Mild leucocytosis."));
            Assert.That(report["status"]!.GetValue<string>(), Is.EqualTo("final"));
        }

        [Test]
        public void DischargeSummary_UsesOnlyTheIcd10CodeWeHold()
        {
            var discharge = new FhirDischarge("Fever for 5 days", "No comorbidities", "Treated with IV fluids", "Appendicectomy", "Acute appendicitis",
                "K35.80", "Unspecified acute appendicitis", "Stable", "Review in 7 days", "Soft diet", "No heavy lifting", new[] { Paracetamol });

            var bundle = AbdmFhir.DischargeSummary(Context, discharge);

            AssertSound(bundle, "DischargeSummaryRecord");
            var coded = Resources(bundle, "Condition").Single(c => ((JsonObject)c["code"]!).ContainsKey("coding"));
            Assert.That(coded["code"]!["coding"]![0]!["code"]!.GetValue<string>(), Is.EqualTo("K35.80"));
            Assert.That(coded["code"]!["coding"]![0]!["system"]!.GetValue<string>(), Is.EqualTo("http://hl7.org/fhir/sid/icd-10"));
            Assert.That(Resources(bundle, "Procedure").Single()["code"]!["text"]!.GetValue<string>(), Is.EqualTo("Appendicectomy"));
            Assert.That(Resources(bundle, "CarePlan").Single()["description"]!.GetValue<string>(), Does.Contain("Review in 7 days").And.Contain("Soft diet"));
            Assert.That(Resources(bundle, "MedicationRequest"), Has.Count.EqualTo(1));

            var sparse = AbdmFhir.DischargeSummary(Context, new FhirDischarge(null, null, null, null, "Dengue fever", null, null, null, null, null, null, Array.Empty<FhirMedicine>()));
            AssertSound(sparse, "DischargeSummaryRecord");
            Assert.That(((JsonObject)Resources(sparse, "Condition").Single()["code"]!).ContainsKey("coding"), Is.False);
        }

        [Test]
        public void WellnessRecord_SendsOnlyTheVitalsThatWereTaken()
        {
            var bundle = AbdmFhir.WellnessRecord(Context, new FhirVitals(120, 80, 76, 37.2m, 98, null, 162, 58.5m, 0));

            AssertSound(bundle, "WellnessRecord");
            var codes = Resources(bundle, "Observation").Select(o => o["code"]!["coding"]![0]!["code"]!.GetValue<string>()).ToList();
            Assert.That(codes, Is.EquivalentTo(new[] { "85354-9", "8867-4", "8310-5", "59408-5", "8302-2", "29463-7" }));
            var pressure = Resources(bundle, "Observation").Single(o => o["code"]!["coding"]![0]!["code"]!.GetValue<string>() == "85354-9");
            Assert.That(pressure["component"]![0]!["valueQuantity"]!["value"]!.GetValue<decimal>(), Is.EqualTo(120m));
            Assert.That(pressure["component"]![1]!["valueQuantity"]!["value"]!.GetValue<decimal>(), Is.EqualTo(80m));
        }

        [Test]
        public void ARecordWithNothingInIt_HasNoContent_SoItIsNotSent()
        {
            Assert.That(AbdmFhir.HasContent(AbdmFhir.WellnessRecord(Context, new FhirVitals(null, null, null, null, null, null, null, null, null))), Is.False);
            Assert.That(AbdmFhir.HasContent(AbdmFhir.Prescription(Context, Array.Empty<FhirMedicine>(), null)), Is.False);
            Assert.That(new FhirVitals(0, 0, null, null, null, null, null, null, null).IsEmpty, Is.True);
        }

        [Test]
        public void ADanglingReference_IsReported()
        {
            var bundle = AbdmFhir.Prescription(Context, new[] { Paracetamol }, null);
            Resources(bundle, "MedicationRequest").Single()["subject"] = new JsonObject { ["reference"] = "urn:uuid:not-in-this-bundle" };

            Assert.That(AbdmFhir.UnresolvedReferences(bundle), Is.EqualTo(new[] { "urn:uuid:not-in-this-bundle" }));
        }

        [Test]
        public void Vitals_AreReadWhateverTheKeyCase()
        {
            var pascal = AbdmRecordSource.ParseVitals("{\"Bp\":{\"Sys\":120,\"Dia\":80},\"Pulse\":76,\"TempC\":37.2,\"Spo2\":98,\"HeightCm\":162,\"WeightKg\":58.5,\"Bmi\":22.3,\"RespiratoryRate\":16}");
            var camel = AbdmRecordSource.ParseVitals("{\"bp\":{\"sys\":\"120\",\"dia\":80},\"pulse\":76}");

            Assert.That(pascal, Is.EqualTo(new FhirVitals(120, 80, 76, 37.2m, 98, 16, 162, 58.5m, 22.3m)));
            Assert.That(camel!.Systolic, Is.EqualTo(120m));
            Assert.That(camel.Pulse, Is.EqualTo(76m));
            Assert.That(AbdmRecordSource.ParseVitals("not json"), Is.Null);
            Assert.That(AbdmRecordSource.ParseVitals(null), Is.Null);
        }
    }
}
