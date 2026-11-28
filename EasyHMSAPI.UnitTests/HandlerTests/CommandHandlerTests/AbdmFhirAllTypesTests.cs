#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using EasyHMSAPI.Application.Services.Fhir;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 (Health_RECORD_CREATION_101 and HIP_INIT_SHARE_CARECONTEXT): an HMIS shares all eight HI types,
    // each structured and with its PDF copy (Invoice structured only). Each bundle here also passed NHA's own
    // FHIR checker (the docs MCP "validate" tool, NRCeS ndhm.in 6.5.0) with no findings when it was written.
    [TestFixture]
    public class AbdmFhirAllTypesTests
    {
        private static readonly DateTime When = new(2026, 11, 20, 5, 30, 0, DateTimeKind.Utc);

        private static readonly FhirContext Printed = new(
            new FhirPatient("P-0001", "Asha Rao", "Female", new DateTime(1990, 8, 15), "91-1234-5678-9012", "asha@sbx"),
            new FhirAuthor("doc-1", "Dr Ravi Kumar", "MCI-12345"),
            new FhirFacility("hosp-1", "City Hospital", "IN2810014366"),
            When, "OPD-abc",
            new FhirPrint("36 years", "9876543210", "MBBS, MD", "General Medicine"));

        private static readonly FhirContext Unprinted = Printed with { Print = null };

        private static readonly FhirMedicine Paracetamol = new("Paracetamol 650 mg", "1 tablet", "TDS", "5 days", "Oral", "After food");

        private static JsonArray Entries(JsonObject bundle) => (JsonArray)bundle["entry"]!;
        private static JsonObject Composition(JsonObject bundle) => (JsonObject)Entries(bundle)[0]!["resource"]!;
        private static List<JsonObject> Resources(JsonObject bundle, string type) =>
            Entries(bundle).Select(e => (JsonObject)e!["resource"]!).Where(r => r["resourceType"]!.GetValue<string>() == type).ToList();
        private static List<string> SectionCodes(JsonObject bundle) =>
            ((JsonArray)Composition(bundle)["section"]!).Select(s => s!["code"]?["coding"]?[0]?["code"]?.GetValue<string>() ?? s!["title"]!.GetValue<string>()).ToList();

        private static void AssertSound(JsonObject bundle, string profile)
        {
            Assert.That(Composition(bundle)["meta"]!["profile"]![0]!.GetValue<string>(), Does.EndWith("/" + profile));
            Assert.That(AbdmFhir.HasContent(bundle), Is.True);
            Assert.That(AbdmFhir.UnresolvedReferences(bundle), Is.Empty);
            Assert.That(bundle.ToJsonString(), Does.Not.Contain(":null").And.Not.Contain(":\"\"").And.Not.Contain(":[]"));
        }

        private static string PdfText(JsonObject bundle)
        {
            var reference = Resources(bundle, "DocumentReference").Single(r => r["content"]![0]!["attachment"]!["contentType"]!.GetValue<string>() == "application/pdf");
            var pdf = Convert.FromBase64String(reference["content"]![0]!["attachment"]!["data"]!.GetValue<string>());
            Assert.That(Encoding.Latin1.GetString(pdf, 0, 8), Is.EqualTo("%PDF-1.4"));
            return Encoding.Latin1.GetString(pdf);
        }

        [Test]
        public void OPConsultation_IsAnOPConsultRecord_WithTheNrcesSectionCodes_AndItsPdf()
        {
            var bundle = AbdmFhir.OPConsultation(Printed, new FhirConsultation("Fever for 3 days", "Diabetic for 5 years", "Temp 101 F", "Viral fever",
                new[] { Paracetamol }, When.AddDays(7), "Review with CBC", "Fluids"));

            AssertSound(bundle, "OPConsultRecord");
            Assert.That(Composition(bundle)["type"]!["coding"]![0]!["code"]!.GetValue<string>(), Is.EqualTo("371530004"));
            Assert.That(SectionCodes(bundle), Is.EqualTo(new[] { "422843007", "371529009", "425044008", "721912009", "390906007", "371530004" }));
            var text = PdfText(bundle);
            foreach (var expected in new[] { "Asha Rao", "36 years", "Female", "9876543210", "91-1234-5678-9012", "asha@sbx", "Dr Ravi Kumar, MBBS, MD", "General Medicine" })
                Assert.That(text, Does.Contain(expected), expected);
        }

        [Test]
        public void OPConsultation_WithNothingClinical_IsNotSent()
        {
            var bundle = AbdmFhir.OPConsultation(Printed, new FhirConsultation(null, " ", null, null, Array.Empty<FhirMedicine>(), null, null, "Rest"));
            Assert.That(AbdmFhir.HasContent(bundle), Is.False);
        }

        [Test]
        public void Prescription_CarriesItsPdfAsABinary_InItsOnlySection()
        {
            var bundle = AbdmFhir.Prescription(Printed, new[] { Paracetamol }, "Viral fever");

            AssertSound(bundle, "PrescriptionRecord");
            var entryTypes = ((JsonArray)Composition(bundle)["section"]![0]!["entry"]!).Select(e => e!["type"]!.GetValue<string>()).ToList();
            Assert.That(entryTypes, Is.EqualTo(new[] { "MedicationRequest", "Binary" }));
            Assert.That(Resources(bundle, "Binary").Single()["contentType"]!.GetValue<string>(), Is.EqualTo("application/pdf"));
        }

        [Test]
        public void WithoutPrintDetails_NoPdfIsAdded()
        {
            var bundle = AbdmFhir.Prescription(Unprinted, new[] { Paracetamol }, "Viral fever");
            Assert.That(Resources(bundle, "Binary"), Is.Empty);
        }

        [Test]
        public void DischargeSummary_UsesTheNrces65SectionCodes_AndCarriesItsPdf()
        {
            var bundle = AbdmFhir.DischargeSummary(Printed with { Reference = "IPD-1" }, new FhirDischarge("Pain abdomen", "2 days", "Uneventful",
                "Laparoscopic appendectomy", "Acute appendicitis", "K35.8", "Acute appendicitis, other", "Stable", "Review in 1 week", "Soft diet", "No lifting", new[] { Paracetamol }));

            AssertSound(bundle, "DischargeSummaryRecord");
            Assert.That(SectionCodes(bundle), Is.EqualTo(new[] { "422843007", "1003642006", "1003640003", "1003606003", "734163000", "373942005" }));
            Assert.That(PdfText(bundle), Does.Contain("Acute appendicitis"));
        }

        [Test]
        public void DiagnosticReport_HoldsTheReportAndItsPdf_TheTwoEntriesItsSectionAllows()
        {
            var bundle = AbdmFhir.DiagnosticReport(Printed, "LAB-1", new[] { "Complete blood count" }, "Normal");

            AssertSound(bundle, "DiagnosticReportRecord");
            var entries = (JsonArray)Composition(bundle)["section"]![0]!["entry"]!;
            Assert.That(entries.Select(e => e!["type"]!.GetValue<string>()), Is.EqualTo(new[] { "DiagnosticReport", "DocumentReference" }));
        }

        [Test]
        public void WellnessRecord_AddsADocumentReferenceSection()
        {
            var bundle = AbdmFhir.WellnessRecord(Printed, new FhirVitals(120, 80, 76, null, null, null, null, null, null));

            AssertSound(bundle, "WellnessRecord");
            Assert.That(SectionCodes(bundle).Last(), Is.EqualTo("Document Reference"));
            Assert.That(PdfText(bundle), Does.Contain("Blood pressure: 120/80 mmHg"));
        }

        [Test]
        public void ImmunizationRecord_ListsEachVaccineGiven_WithItsDose()
        {
            var bundle = AbdmFhir.ImmunizationRecord(Printed, new[] { new FhirVaccine("Hepatitis B", "given", When, 2, null), new FhirVaccine("Tetanus toxoid", null, null, null, null) });

            AssertSound(bundle, "ImmunizationRecord");
            var shots = Resources(bundle, "Immunization");
            Assert.That(shots.Select(s => s["vaccineCode"]!["text"]!.GetValue<string>()), Is.EqualTo(new[] { "Hepatitis B", "Tetanus toxoid" }));
            Assert.That(shots.All(s => s["status"]!.GetValue<string>() == "completed"), Is.True);
            Assert.That(shots[0]["protocolApplied"]![0]!["doseNumberPositiveInt"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(shots[1]["occurrenceDateTime"]!.GetValue<string>(), Is.EqualTo("2026-11-20T05:30:00Z"), "no date on the pad: the visit's date");
            Assert.That(SectionCodes(bundle), Is.EqualTo(new[] { "41000179103" }));
        }

        [Test]
        public void HealthDocument_SendsTheUploadedFileAsIs()
        {
            var bytes = Encoding.ASCII.GetBytes("%PDF-1.4 scanned report");
            var bundle = AbdmFhir.HealthDocument(Unprinted, new[] { new FhirFile("Old CBC report", "application/pdf", bytes, When.AddDays(-30)), new FhirFile("empty", "image/png", Array.Empty<byte>(), null) });

            AssertSound(bundle, "HealthDocumentRecord");
            var attachment = Resources(bundle, "DocumentReference").Single()["content"]![0]!["attachment"]!;
            Assert.That(Convert.FromBase64String(attachment["data"]!.GetValue<string>()), Is.EqualTo(bytes));
            Assert.That(attachment["title"]!.GetValue<string>(), Is.EqualTo("Old CBC report"));
            Assert.That(Composition(bundle)["type"]!["coding"]![0]!["code"]!.GetValue<string>(), Is.EqualTo("419891008"));
        }

        [Test]
        public void Invoice_PricesEachLine_AndTotalsTheBill()
        {
            var lines = new[]
            {
                new FhirInvoiceLine("Consultation fee", "CONSULTATION", 1, 500, 0, 0, 0, 0),
                new FhirInvoiceLine("Paracetamol 650 mg", "PHARMACY", 15, 2, 5, 0.9m, 0.9m, 0),
                new FhirInvoiceLine("Dressing", "PROCEDURE", 1, 100, 0, 0, 0, 18)
            };
            var (net, tax) = AbdmRecordSource.InvoiceTotals(lines);
            Assert.That(net, Is.EqualTo(625m));
            Assert.That(tax, Is.EqualTo(19.8m));

            var bundle = AbdmFhir.Invoice(Unprinted, new FhirInvoice("INV-91", "OPD", lines, net, tax, net + tax));

            AssertSound(bundle, "InvoiceRecord");
            Assert.That(Composition(bundle)["type"]!["text"]!.GetValue<string>(), Is.EqualTo("Invoice Record"));
            var invoice = Resources(bundle, "Invoice").Single();
            Assert.That(invoice["totalNet"]!["value"]!.GetValue<decimal>(), Is.EqualTo(625m));
            Assert.That(invoice["totalGross"]!["value"]!.GetValue<decimal>(), Is.EqualTo(644.8m));
            Assert.That(invoice["type"]!["coding"]![0]!["code"]!.GetValue<string>(), Is.EqualTo("03"));
            var charges = Resources(bundle, "ChargeItem").Select(c => c["code"]!["coding"]![0]!["code"]!.GetValue<string>()).ToList();
            Assert.That(charges, Is.EqualTo(new[] { "00", "05", "99" }));
            var pharmacy = invoice["lineItem"]![1]!["priceComponent"]!.AsArray().Select(p => p!["type"]!.GetValue<string>()).ToList();
            Assert.That(pharmacy, Is.EqualTo(new[] { "base", "discount", "tax", "tax" }));
            var igst = invoice["lineItem"]![2]!["priceComponent"]![1]!;
            Assert.That(igst["code"]!["text"]!.GetValue<string>(), Is.EqualTo("IGST"), "no NRCeS code for IGST: text only");
        }

        [Test]
        public void Vaccines_OnlyThoseGivenAreARecord()
        {
            var vaccines = AbdmRecordSource.ParseVaccines(
                "[{\"name\":\"HB\",\"status\":\"advised\",\"date\":\"2025-12-30T00:00:00\"},{\"name\":\"BCG\",\"status\":\"given\",\"date\":\"2025-12-01T00:00:00\",\"doseNumber\":1},{\"name\":\" \",\"status\":\"given\"}]");

            Assert.That(vaccines.Select(v => v.Name), Is.EqualTo(new[] { "BCG" }));
            Assert.That(vaccines[0].DoseNumber, Is.EqualTo(1));
            Assert.That(vaccines[0].Date, Is.EqualTo(new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(AbdmRecordSource.ParseVaccines("not json"), Is.Empty);
            Assert.That(AbdmRecordSource.ParseVaccines(null), Is.Empty);
        }

        [Test]
        public void Age_ComesFromTheDateOfBirth_ElseTheTypedAge()
        {
            Assert.That(AbdmRecordSource.AgeText(DateTime.UtcNow.Date.AddYears(-36).AddDays(-1), null, null), Is.EqualTo("36 years"));
            Assert.That(AbdmRecordSource.AgeText(DateTime.UtcNow.Date.AddMonths(-3).AddDays(-1), null, null), Is.EqualTo("3 months"));
            Assert.That(AbdmRecordSource.AgeText(null, 40, "Years"), Is.EqualTo("40 years"));
            Assert.That(AbdmRecordSource.AgeText(null, null, null), Is.Null);
        }

        [Test]
        public void UploadedFiles_OnlyPdfsAndImagesAreShared()
        {
            Assert.That(AbdmDocumentFiles.ContentTypeFor("report.PDF"), Is.EqualTo("application/pdf"));
            Assert.That(AbdmDocumentFiles.ContentTypeFor("xray.jpeg"), Is.EqualTo("image/jpeg"));
            Assert.That(AbdmDocumentFiles.ContentTypeFor("scan.png"), Is.EqualTo("image/png"));
            Assert.That(AbdmDocumentFiles.ContentTypeFor("notes.docx"), Is.Null);
            Assert.That(AbdmDocumentFiles.ContentTypeFor(null), Is.Null);
        }

        [Test]
        public void Pdf_IsAValidFile_ThatRunsOntoMorePages()
        {
            var longText = string.Join(" ", Enumerable.Repeat("word", 3000));
            var pdf = AbdmPdf.Render(new AbdmPdfDocument("City Hospital", "Test", new (string, string?)[] { ("Patient", "Asha (Rao)"), ("Empty", null) },
                new[] { new AbdmPdfSection("Notes", new[] { longText, "अशा" }) }));
            var text = Encoding.Latin1.GetString(pdf);

            Assert.That(text, Does.StartWith("%PDF-1.4"));
            Assert.That(text.TrimEnd(), Does.EndWith("%%EOF"));
            var pages = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Count (\d+)").Groups[1].Value);
            Assert.That(pages, Is.GreaterThan(1));
            Assert.That(text, Does.Contain("Asha \\(Rao\\)"), "brackets are escaped");
            Assert.That(text, Does.Not.Contain("Empty"), "a label with no value is left out");
            Assert.That(text, Does.Contain("(???)"), "text outside Latin-1 prints as ? and keeps the file valid");
            // Every xref offset points at the object it names.
            var xref = text.LastIndexOf("\nxref\n", StringComparison.Ordinal) + 1;
            var offsets = text[xref..].Split('\n').Skip(3).TakeWhile(l => l.EndsWith(" n ")).Select(l => int.Parse(l[..10])).ToList();
            Assert.That(offsets, Has.Count.EqualTo(4 + 2 * pages));
            for (var i = 0; i < offsets.Count; i++)
                Assert.That(text.Substring(offsets[i], $"{i + 1} 0 obj".Length), Is.EqualTo($"{i + 1} 0 obj"));
        }

        [Test]
        public void Wrap_KeepsEachLineInsideTheWidth()
        {
            var lines = AbdmPdf.Wrap(string.Join(" ", Enumerable.Repeat("Paracetamol", 40)) + " " + new string('x', 200), 10f, false, 300f);
            Assert.That(lines, Has.Count.GreaterThan(5));
            Assert.That(lines.All(l => AbdmPdf.Measure(l, 10f, false) <= 300f), Is.True);
        }
    }
}
