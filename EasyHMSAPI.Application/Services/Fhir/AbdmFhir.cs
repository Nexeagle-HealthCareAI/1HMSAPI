using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace EasyHMSAPI.Application.Services.Fhir
{
    public sealed record FhirPatient(string PatientId, string? Name, string? Gender, DateTime? BirthDate, string? AbhaNumber, string? AbhaAddress);
    public sealed record FhirAuthor(string Id, string? Name, string? RegistrationNumber);
    public sealed record FhirFacility(string Id, string Name, string? HipId);
    public sealed record FhirMedicine(string? Name, string? Dosage, string? Frequency, string? Duration, string? Route, string? Instructions);

    /// <summary>What every record needs: whose it is, who wrote it, where, when, and our reference.</summary>
    public sealed record FhirContext(FhirPatient Patient, FhirAuthor Author, FhirFacility Facility, DateTime WhenUtc, string Reference);

    public sealed record FhirDischarge(
        string? ChiefComplaint, string? History, string? CourseInHospital, string? Procedures, string? FinalDiagnosis,
        string? Icd10Code, string? Icd10Name, string? ConditionAtDischarge, string? FollowUp, string? Diet, string? Activity,
        IReadOnlyList<FhirMedicine> Medicines);

    public sealed record FhirVitals(decimal? Systolic, decimal? Diastolic, decimal? Pulse, decimal? TempC, decimal? Spo2,
        decimal? RespiratoryRate, decimal? HeightCm, decimal? WeightKg, decimal? Bmi)
    {
        public bool IsEmpty => new[] { Systolic, Diastolic, Pulse, TempC, Spo2, RespiratoryRate, HeightCm, WeightKg, Bmi }.All(v => v == null || v == 0);
    }

    /// <summary>
    /// ABDM FHIR document bundles (NRCES profiles, R4) built as plain JSON, as the abdm-fhir skill
    /// allows when the codebase has no FHIR library. Pure: our data in, a bundle out.
    /// Rules kept here: the Composition is the first entry; every reference points at a resource
    /// in the same bundle; nothing is written as an empty value; and no clinical code is invented —
    /// free text stays text, and only codes we actually hold (ICD-10 on a discharge) or that name a
    /// fixed measurement (LOINC for vitals) or document type (SNOMED CT) are used.
    /// Not yet run through the official HL7 validator with the NRCES guide.
    /// </summary>
    public static class AbdmFhir
    {
        private const string Nrces = "https://nrces.in/ndhm/fhir/r4/StructureDefinition/";
        private const string Snomed = "http://snomed.info/sct";
        private const string Loinc = "http://loinc.org";
        private const string Ucum = "http://unitsofmeasure.org";
        private const string OurSystem = "https://nexeagle.com/1hms/";

        // ---- the four record types -----------------------------------------------------------

        public static JsonObject Prescription(FhirContext context, IEnumerable<FhirMedicine> medicines, string? diagnosis)
        {
            var bundle = new Builder(context, "PrescriptionRecord", "440545006", "Prescription record", "Prescription");
            var entries = medicines.Where(m => !string.IsNullOrWhiteSpace(m.Name)).Select(m => bundle.Add(MedicationRequest(bundle, m))).ToList();
            if (!string.IsNullOrWhiteSpace(diagnosis))
                entries.Add(bundle.Add(Condition(bundle, diagnosis!, null, null)));
            bundle.Section("Prescription record", "440545006", "Prescription record", entries, null);
            return bundle.Build();
        }

        public static JsonObject DiagnosticReport(FhirContext context, string reportNumber, IEnumerable<string> testNames, string? conclusion)
        {
            var bundle = new Builder(context, "DiagnosticReportRecord", "721981007", "Diagnostic studies report", "Diagnostic Report");
            var tests = string.Join(", ", testNames.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct());
            var report = Resource("DiagnosticReport", "DiagnosticReportLab");
            report["identifier"] = new JsonArray(Identifier(OurSystem + "lab-report", reportNumber));
            report["status"] = "final";
            report["category"] = new JsonArray(Concept("http://terminology.hl7.org/CodeSystem/v2-0074", "LAB", "Laboratory"));
            report["code"] = new JsonObject { ["text"] = string.IsNullOrEmpty(tests) ? "Laboratory report" : tests };
            report["subject"] = bundle.PatientReference();
            report["effectiveDateTime"] = Instant(context.WhenUtc);
            report["issued"] = Instant(context.WhenUtc);
            report["performer"] = new JsonArray(bundle.FacilityReference());
            report["resultsInterpreter"] = new JsonArray(bundle.AuthorReference());
            Put(report, "conclusion", conclusion);
            bundle.Section("Diagnostic report", "721981007", "Diagnostic studies report", new List<JsonObject> { bundle.Add(report) }, null);
            return bundle.Build();
        }

        public static JsonObject DischargeSummary(FhirContext context, FhirDischarge discharge)
        {
            var bundle = new Builder(context, "DischargeSummaryRecord", "373942005", "Discharge summary", "Discharge Summary");

            if (!string.IsNullOrWhiteSpace(discharge.ChiefComplaint))
                bundle.Section("Chief complaints", "422843007", "Chief complaint section",
                    new List<JsonObject> { bundle.Add(Condition(bundle, discharge.ChiefComplaint!, null, null)) }, null);

            var diagnosis = !string.IsNullOrWhiteSpace(discharge.FinalDiagnosis) ? discharge.FinalDiagnosis : discharge.Icd10Name;
            var history = JoinText(("History", discharge.History), ("Course in hospital", discharge.CourseInHospital), ("Condition at discharge", discharge.ConditionAtDischarge));
            if (!string.IsNullOrWhiteSpace(diagnosis))
                bundle.Section("Medical history", "371529009", "History and physical report",
                    new List<JsonObject> { bundle.Add(Condition(bundle, diagnosis!, discharge.Icd10Code, discharge.Icd10Name)) }, history);

            if (!string.IsNullOrWhiteSpace(discharge.Procedures))
            {
                var procedure = Resource("Procedure", "Procedure");
                procedure["status"] = "completed";
                procedure["code"] = new JsonObject { ["text"] = discharge.Procedures!.Trim() };
                procedure["subject"] = bundle.PatientReference();
                bundle.Section("Procedures", "371525003", "Clinical procedure report", new List<JsonObject> { bundle.Add(procedure) }, null);
            }

            var medicines = discharge.Medicines.Where(m => !string.IsNullOrWhiteSpace(m.Name)).Select(m => bundle.Add(MedicationRequest(bundle, m))).ToList();
            if (medicines.Count > 0)
                bundle.Section("Medications", "721912009", "Medication summary document", medicines, null);

            var plan = JoinText(("Follow-up", discharge.FollowUp), ("Diet", discharge.Diet), ("Activity", discharge.Activity));
            if (plan != null)
            {
                var carePlan = Resource("CarePlan", "CarePlan");
                carePlan["status"] = "active";
                carePlan["intent"] = "plan";
                carePlan["title"] = "Advice on discharge";
                carePlan["description"] = plan;
                carePlan["subject"] = bundle.PatientReference();
                bundle.Section("Care plan", "734163000", "Care plan", new List<JsonObject> { bundle.Add(carePlan) }, null);
            }
            return bundle.Build();
        }

        public static JsonObject WellnessRecord(FhirContext context, FhirVitals vitals)
        {
            var bundle = new Builder(context, "WellnessRecord", null, null, "Wellness Record");

            var signs = new List<JsonObject>();
            if (Has(vitals.Systolic) && Has(vitals.Diastolic))
            {
                var pressure = Observation(bundle, "ObservationVitalSigns", "vital-signs", "85354-9", "Blood pressure panel with all children optional");
                pressure["component"] = new JsonArray(
                    new JsonObject { ["code"] = Concept(Loinc, "8480-6", "Systolic blood pressure"), ["valueQuantity"] = Quantity(vitals.Systolic!.Value, "mm[Hg]", "mm[Hg]") },
                    new JsonObject { ["code"] = Concept(Loinc, "8462-4", "Diastolic blood pressure"), ["valueQuantity"] = Quantity(vitals.Diastolic!.Value, "mm[Hg]", "mm[Hg]") });
                signs.Add(bundle.Add(pressure));
            }
            AddQuantity(bundle, signs, "ObservationVitalSigns", "vital-signs", vitals.Pulse, "8867-4", "Heart rate", "beats/minute", "/min");
            AddQuantity(bundle, signs, "ObservationVitalSigns", "vital-signs", vitals.RespiratoryRate, "9279-1", "Respiratory rate", "breaths/minute", "/min");
            AddQuantity(bundle, signs, "ObservationVitalSigns", "vital-signs", vitals.TempC, "8310-5", "Body temperature", "Cel", "Cel");
            AddQuantity(bundle, signs, "ObservationVitalSigns", "vital-signs", vitals.Spo2, "59408-5", "Oxygen saturation in Arterial blood by Pulse oximetry", "%", "%");
            if (signs.Count > 0)
                bundle.Section("Vital Signs", null, null, signs, null);

            var body = new List<JsonObject>();
            AddQuantity(bundle, body, "ObservationBodyMeasurement", "vital-signs", vitals.HeightCm, "8302-2", "Body height", "cm", "cm");
            AddQuantity(bundle, body, "ObservationBodyMeasurement", "vital-signs", vitals.WeightKg, "29463-7", "Body weight", "kg", "kg");
            AddQuantity(bundle, body, "ObservationBodyMeasurement", "vital-signs", vitals.Bmi, "39156-5", "Body mass index (BMI) [Ratio]", "kg/m2", "kg/m2");
            if (body.Count > 0)
                bundle.Section("Body Measurement", null, null, body, null);
            return bundle.Build();
        }

        // ---- checks --------------------------------------------------------------------------

        /// <summary>False for a bundle whose Composition has no section: there is nothing in it to send.</summary>
        public static bool HasContent(JsonObject bundle) =>
            bundle["entry"] is JsonArray { Count: > 0 } entries && entries[0]?["resource"]?["section"] is JsonArray { Count: > 0 };

        /// <summary>Every reference in the bundle that does not point at one of its own entries.
        /// Empty means the bundle stands on its own, which is what a document bundle must do.</summary>
        public static IReadOnlyList<string> UnresolvedReferences(JsonObject bundle)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (bundle["entry"] is JsonArray entries)
            {
                foreach (var entry in entries)
                {
                    var url = entry?["fullUrl"]?.GetValue<string>();
                    if (url != null) known.Add(url);
                }
            }
            var missing = new List<string>();
            CollectReferences(bundle, known, missing);
            return missing;
        }

        private static void CollectReferences(JsonNode? node, HashSet<string> known, List<string> missing)
        {
            if (node is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    if (pair.Key == "reference" && pair.Value is JsonValue value && value.TryGetValue<string>(out var reference))
                    {
                        if (!known.Contains(reference)) missing.Add(reference);
                    }
                    else
                        CollectReferences(pair.Value, known, missing);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                    CollectReferences(item, known, missing);
            }
        }

        // ---- resources -----------------------------------------------------------------------

        private static JsonObject MedicationRequest(Builder bundle, FhirMedicine medicine)
        {
            var request = Resource("MedicationRequest", "MedicationRequest");
            request["status"] = "active";
            request["intent"] = "order";
            request["medicationCodeableConcept"] = new JsonObject { ["text"] = medicine.Name!.Trim() };
            request["subject"] = bundle.PatientReference();
            request["authoredOn"] = Instant(bundle.Context.WhenUtc);
            request["requester"] = bundle.AuthorReference();
            var dosage = new JsonObject();
            Put(dosage, "text", JoinWords(medicine.Dosage, medicine.Frequency, medicine.Duration, medicine.Instructions));
            if (!string.IsNullOrWhiteSpace(medicine.Route))
                dosage["route"] = new JsonObject { ["text"] = medicine.Route!.Trim() };
            if (dosage.Count > 0)
                request["dosageInstruction"] = new JsonArray(dosage);
            return request;
        }

        private static JsonObject Condition(Builder bundle, string text, string? icd10Code, string? icd10Name)
        {
            var condition = Resource("Condition", "Condition");
            condition["clinicalStatus"] = Concept("http://terminology.hl7.org/CodeSystem/condition-clinical", "active", "Active");
            var code = new JsonObject { ["text"] = text.Trim() };
            if (!string.IsNullOrWhiteSpace(icd10Code))
            {
                var coding = new JsonObject { ["system"] = "http://hl7.org/fhir/sid/icd-10", ["code"] = icd10Code!.Trim() };
                Put(coding, "display", icd10Name);
                code["coding"] = new JsonArray(coding);
            }
            condition["code"] = code;
            condition["subject"] = bundle.PatientReference();
            return condition;
        }

        private static JsonObject Observation(Builder bundle, string profile, string category, string loincCode, string display)
        {
            var observation = Resource("Observation", profile);
            observation["status"] = "final";
            observation["category"] = new JsonArray(Concept("http://terminology.hl7.org/CodeSystem/observation-category", category, "Vital Signs"));
            observation["code"] = Concept(Loinc, loincCode, display);
            observation["subject"] = bundle.PatientReference();
            observation["effectiveDateTime"] = Instant(bundle.Context.WhenUtc);
            observation["performer"] = new JsonArray(bundle.AuthorReference());
            return observation;
        }

        private static void AddQuantity(Builder bundle, List<JsonObject> into, string profile, string category, decimal? value, string loincCode, string display, string unit, string ucumCode)
        {
            if (!Has(value))
                return;
            var observation = Observation(bundle, profile, category, loincCode, display);
            observation["valueQuantity"] = Quantity(value!.Value, unit, ucumCode);
            into.Add(bundle.Add(observation));
        }

        // ---- small pieces --------------------------------------------------------------------

        private static bool Has(decimal? value) => value != null && value.Value > 0;

        private static JsonObject Resource(string type, string profile) => new()
        {
            ["resourceType"] = type,
            ["id"] = Guid.NewGuid().ToString(),
            ["meta"] = new JsonObject { ["profile"] = new JsonArray(Nrces + profile) }
        };

        private static JsonObject Concept(string system, string code, string display) => new()
        {
            ["coding"] = new JsonArray(new JsonObject { ["system"] = system, ["code"] = code, ["display"] = display }),
            ["text"] = display
        };

        private static JsonObject Quantity(decimal value, string unit, string ucumCode) => new()
        {
            ["value"] = value,
            ["unit"] = unit,
            ["system"] = Ucum,
            ["code"] = ucumCode
        };

        private static JsonObject Identifier(string system, string value) => new() { ["system"] = system, ["value"] = value };

        private static string Instant(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        private static void Put(JsonObject target, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target[name] = value.Trim();
        }

        private static string? JoinWords(params string?[] parts)
        {
            var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
            return kept.Count == 0 ? null : string.Join(", ", kept);
        }

        private static string? JoinText(params (string Label, string? Text)[] parts)
        {
            var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p.Text)).Select(p => $"{p.Label}: {p.Text!.Trim()}").ToList();
            return kept.Count == 0 ? null : string.Join("\n", kept);
        }

        private static JsonObject Narrative(string text) => new()
        {
            ["status"] = "generated",
            ["div"] = "<div xmlns=\"http://www.w3.org/1999/xhtml\">" + WebUtility.HtmlEncode(text).Replace("\n", "<br/>") + "</div>"
        };

        private static string FhirGender(string? value) => AbdmCareContextRules.GenderCode(value) switch
        {
            "M" => "male",
            "F" => "female",
            "O" => "other",
            _ => "unknown"
        };

        /// <summary>Holds the bundle while it is put together: the Composition, the three people
        /// and places every record names, and each resource added along the way.</summary>
        private sealed class Builder
        {
            private readonly List<JsonObject> _resources = new();
            private readonly JsonArray _sections = new();
            private readonly JsonObject _composition;
            private readonly JsonObject _patient;
            private readonly JsonObject _author;
            private readonly JsonObject _facility;

            public FhirContext Context { get; }

            public Builder(FhirContext context, string compositionProfile, string? typeCode, string? typeDisplay, string title)
            {
                Context = context;

                _patient = Resource("Patient", "Patient");
                var identifiers = new JsonArray(new JsonObject
                {
                    ["type"] = Concept("http://terminology.hl7.org/CodeSystem/v2-0203", "MR", "Medical record number"),
                    ["system"] = OurSystem + "patient",
                    ["value"] = context.Patient.PatientId
                });
                if (!string.IsNullOrWhiteSpace(context.Patient.AbhaNumber))
                    identifiers.Add(Identifier("https://healthid.ndhm.gov.in/health-number", context.Patient.AbhaNumber!.Trim()));
                if (!string.IsNullOrWhiteSpace(context.Patient.AbhaAddress))
                    identifiers.Add(Identifier("https://healthid.ndhm.gov.in/phr-address", context.Patient.AbhaAddress!.Trim()));
                _patient["identifier"] = identifiers;
                _patient["name"] = new JsonArray(new JsonObject { ["text"] = DisplayName(context.Patient.Name, context.Patient.PatientId) });
                _patient["gender"] = FhirGender(context.Patient.Gender);
                if (context.Patient.BirthDate != null)
                    _patient["birthDate"] = context.Patient.BirthDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                _author = Resource("Practitioner", "Practitioner");
                if (!string.IsNullOrWhiteSpace(context.Author.RegistrationNumber))
                    _author["identifier"] = new JsonArray(new JsonObject
                    {
                        ["type"] = Concept("http://terminology.hl7.org/CodeSystem/v2-0203", "MD", "Medical License number"),
                        ["system"] = "https://doctor.ndhm.gov.in",
                        ["value"] = context.Author.RegistrationNumber!.Trim()
                    });
                _author["name"] = new JsonArray(new JsonObject { ["text"] = DisplayName(context.Author.Name, "Treating doctor") });

                _facility = Resource("Organization", "Organization");
                if (!string.IsNullOrWhiteSpace(context.Facility.HipId))
                    _facility["identifier"] = new JsonArray(new JsonObject
                    {
                        ["type"] = Concept("http://terminology.hl7.org/CodeSystem/v2-0203", "PRN", "Provider number"),
                        ["system"] = "https://facility.ndhm.gov.in",
                        ["value"] = context.Facility.HipId!.Trim()
                    });
                _facility["name"] = context.Facility.Name;

                _composition = Resource("Composition", compositionProfile);
                _composition["language"] = "en-IN";
                _composition["text"] = Narrative($"{title} for {DisplayName(context.Patient.Name, context.Patient.PatientId)}");
                _composition["identifier"] = Identifier(OurSystem + "care-context", context.Reference);
                _composition["status"] = "final";
                _composition["type"] = typeCode == null
                    ? new JsonObject { ["text"] = title }
                    : Concept(Snomed, typeCode, typeDisplay!);
                _composition["subject"] = PatientReference();
                _composition["date"] = Instant(context.WhenUtc);
                _composition["author"] = new JsonArray(AuthorReference());
                _composition["title"] = title;
                _composition["custodian"] = FacilityReference();
            }

            public JsonObject PatientReference() => Reference(_patient, DisplayName(Context.Patient.Name, Context.Patient.PatientId));
            public JsonObject AuthorReference() => Reference(_author, DisplayName(Context.Author.Name, "Treating doctor"));
            public JsonObject FacilityReference() => Reference(_facility, Context.Facility.Name);

            /// <summary>Adds a resource to the bundle and hands it back, so it can be put in a section.</summary>
            public JsonObject Add(JsonObject resource)
            {
                _resources.Add(resource);
                return resource;
            }

            public void Section(string title, string? code, string? display, List<JsonObject> entries, string? narrative)
            {
                if (entries.Count == 0)
                    return;
                var section = new JsonObject { ["title"] = title };
                if (code != null)
                    section["code"] = Concept(Snomed, code, display!);
                if (!string.IsNullOrWhiteSpace(narrative))
                    section["text"] = Narrative(narrative!);
                var references = new JsonArray();
                foreach (var entry in entries)
                    references.Add(Reference(entry, null));
                section["entry"] = references;
                _sections.Add(section);
            }

            public JsonObject Build()
            {
                // FHIR has no empty arrays: a record with nothing to say is not built at all (see HasContent).
                if (_sections.Count > 0)
                    _composition["section"] = _sections;
                var now = Instant(DateTime.UtcNow);
                var id = Guid.NewGuid().ToString();
                var entries = new JsonArray();
                foreach (var resource in new[] { _composition, _patient, _author, _facility }.Concat(_resources))
                    entries.Add(new JsonObject { ["fullUrl"] = FullUrl(resource), ["resource"] = resource });
                return new JsonObject
                {
                    ["resourceType"] = "Bundle",
                    ["id"] = id,
                    ["meta"] = new JsonObject
                    {
                        ["versionId"] = "1",
                        ["lastUpdated"] = now,
                        ["profile"] = new JsonArray(Nrces + "DocumentBundle"),
                        ["security"] = new JsonArray(new JsonObject
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/v3-Confidentiality",
                            ["code"] = "V",
                            ["display"] = "very restricted"
                        })
                    },
                    ["identifier"] = Identifier(OurSystem + "bundle", id),
                    ["type"] = "document",
                    ["timestamp"] = now,
                    ["entry"] = entries
                };
            }

            private static string FullUrl(JsonObject resource) => "urn:uuid:" + resource["id"]!.GetValue<string>();

            private static JsonObject Reference(JsonObject resource, string? display)
            {
                var reference = new JsonObject { ["reference"] = FullUrl(resource) };
                Put(reference, "display", display);
                return reference;
            }

            private static string DisplayName(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        }
    }
}
