using System.Globalization;
using System.Text.Json;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services.Fhir
{
    /// <summary>One record ready to send: its ABDM type and the FHIR bundle as JSON.</summary>
    public sealed record AbdmBuiltRecord(string HiType, string Json);

    public sealed class AbdmBuiltRecords
    {
        /// <summary>When the visit or admission happened; a consent's date range is checked against it.</summary>
        public DateTime WhenUtc { get; init; }
        public IReadOnlyList<AbdmBuiltRecord> Records { get; init; } = Array.Empty<AbdmBuiltRecord>();
    }

    public interface IAbdmRecordSource
    {
        /// <summary>Builds the FHIR bundles for one care context, limited to the HI types allowed.
        /// Null when the visit behind the care context no longer exists.</summary>
        Task<AbdmBuiltRecords?> BuildAsync(AbdmCareContext careContext, IReadOnlyCollection<string> allowedHiTypes, CancellationToken cancellationToken);
    }

    /// <summary>Reads a visit's or an admission's records and hands them to <see cref="AbdmFhir"/>.
    /// All eight ABDM record types, each structured and with its PDF copy (Invoice: structured only;
    /// Health Document: the uploaded file itself).</summary>
    public sealed class AbdmRecordSource : IAbdmRecordSource
    {
        private readonly AppDbContext _db;
        private readonly IAbdmDocumentFiles? _files;
        private readonly string _attachmentsContainer;
        private readonly string _admissionDocumentsContainer;

        public AbdmRecordSource(AppDbContext db, IAbdmDocumentFiles? files = null, IConfiguration? configuration = null)
        {
            _db = db;
            _files = files;
            _attachmentsContainer = configuration?["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
            _admissionDocumentsContainer = configuration?["BlobStorage:AdmissionDocumentsContainer"] ?? string.Empty;
        }

        public async Task<AbdmBuiltRecords?> BuildAsync(AbdmCareContext careContext, IReadOnlyCollection<string> allowedHiTypes, CancellationToken cancellationToken)
        {
            var hospitalId = careContext.HospitalId;
            bool Allowed(string hiType) => allowedHiTypes.Contains(hiType, StringComparer.OrdinalIgnoreCase);

            var registration = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.PatientId == careContext.PatientId)
                .Select(p => new { p.FullName, p.Sex, p.DateOfBirth, p.Age, p.AgeUnit, p.Mobile })
                .FirstOrDefaultAsync(cancellationToken);
            var age = AgeText(registration?.DateOfBirth, registration?.Age, registration?.AgeUnit);
            var mobile = AbdmCareContextRules.Mobile10(registration?.Mobile);
            var patient = new FhirPatient(careContext.PatientId, registration?.FullName, registration?.Sex, registration?.DateOfBirth, careContext.AbhaNumber, careContext.AbhaAddress);

            var hospitalName = await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == hospitalId).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
            var hipId = await _db.AbdmFacilities.AsNoTracking().Where(f => f.HospitalId == hospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
            var facility = new FhirFacility(hospitalId.ToString(), string.IsNullOrWhiteSpace(hospitalName) ? "Hospital" : hospitalName!, hipId);

            var records = new List<AbdmBuiltRecord>();
            void Add(string hiType, System.Text.Json.Nodes.JsonObject bundle)
            {
                if (AbdmFhir.HasContent(bundle))
                    records.Add(new AbdmBuiltRecord(hiType, bundle.ToJsonString()));
            }

            if (careContext.SourceType == AbdmCareContextSource.Opd)
            {
                var appointment = await _db.Appointments.AsNoTracking()
                    .Where(a => a.ApptId == careContext.SourceId && a.HospitalId == hospitalId)
                    .Select(a => new { a.ApptDate, a.DoctorId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (appointment == null)
                    return null;
                var (author, print) = await AuthorAsync(appointment.DoctorId, null, age, mobile, cancellationToken);
                var context = new FhirContext(patient, author, facility, appointment.ApptDate, careContext.ReferenceNumber, print);

                var prescription = await _db.Prescription.AsNoTracking()
                    .Where(p => p.ApptId == careContext.SourceId && p.HospitalId == hospitalId)
                    .OrderByDescending(p => p.UpdatedAt)
                    .Select(p => new
                    {
                        p.PrescriptionId, p.Diagnosis, p.ChiefComplaint, p.History, p.Comorbidity, p.Examination, p.SystemicExamination,
                        p.Immunizations, p.FollowUpDate, p.FollowUpNotes, p.NonPharmacologicalAdvice
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                var medicines = prescription == null
                    ? new List<FhirMedicine>()
                    : await _db.PrescriptionMedicine.AsNoTracking()
                        .Where(m => m.PrescriptionId == prescription.PrescriptionId)
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => new FhirMedicine(m.MedicineName, m.Dosage, m.Frequency, m.Durations, m.Route, m.Instructions))
                        .ToListAsync(cancellationToken);

                if (prescription != null && Allowed("OPConsultation"))
                {
                    Add("OPConsultation", AbdmFhir.OPConsultation(context, new FhirConsultation(
                        prescription.ChiefComplaint,
                        JoinLines(prescription.History, string.IsNullOrWhiteSpace(prescription.Comorbidity) ? null : "Comorbidities: " + prescription.Comorbidity!.Trim()),
                        JoinLines(prescription.Examination, prescription.SystemicExamination),
                        prescription.Diagnosis, medicines, prescription.FollowUpDate, prescription.FollowUpNotes, prescription.NonPharmacologicalAdvice)));
                }

                if (prescription != null && Allowed("Prescription"))
                    Add("Prescription", AbdmFhir.Prescription(context, medicines, prescription.Diagnosis));

                if (prescription != null && Allowed("ImmunizationRecord"))
                {
                    var vaccines = ParseVaccines(prescription.Immunizations);
                    if (vaccines.Count > 0)
                        Add("ImmunizationRecord", AbdmFhir.ImmunizationRecord(context, vaccines));
                }

                if (Allowed("HealthDocumentRecord"))
                {
                    var attachments = await _db.PrescriptionAttachments.AsNoTracking()
                        .Where(a => a.ApptId == careContext.SourceId && a.HospitalId == hospitalId && a.StorageUrl != null)
                        .OrderBy(a => a.UploadedAt)
                        .Select(a => new { a.AttachmentId, a.FileName, a.ReportType, a.StorageUrl, a.UploadedAt })
                        .ToListAsync(cancellationToken);
                    var files = new List<FhirFile>();
                    foreach (var a in attachments)
                    {
                        var file = await ReadFileAsync(_attachmentsContainer, $"{a.AttachmentId}_", a.StorageUrl, a.ReportType ?? a.FileName ?? "Document", a.FileName, a.UploadedAt, cancellationToken);
                        if (file != null) files.Add(file);
                    }
                    if (files.Count > 0)
                        Add("HealthDocumentRecord", AbdmFhir.HealthDocument(context, files));
                }

                if (Allowed("Invoice"))
                    await AddInvoicesAsync(context, hospitalId, careContext.SourceId, "OPD", Add, cancellationToken);

                if (Allowed("WellnessRecord"))
                {
                    var vitalsJson = await _db.AppointmentVitals.AsNoTracking()
                        .Where(v => v.ApptId == careContext.SourceId && v.HospitalId == hospitalId)
                        .OrderByDescending(v => v.RecordedAt)
                        .Select(v => v.VitalsJson)
                        .FirstOrDefaultAsync(cancellationToken);
                    var vitals = ParseVitals(vitalsJson);
                    if (vitals != null && !vitals.IsEmpty)
                        Add("WellnessRecord", AbdmFhir.WellnessRecord(context, vitals));
                }

                if (Allowed("DiagnosticReport"))
                {
                    var encounterIds = await _db.Encounter.AsNoTracking()
                        .Where(e => e.HospitalId == hospitalId && e.SourceId == careContext.SourceId)
                        .Select(e => (Guid?)e.EncounterId)
                        .ToListAsync(cancellationToken);
                    if (encounterIds.Count > 0)
                    {
                        var orderIds = await _db.PathologyOrder.AsNoTracking()
                            .Where(o => o.HospitalId == hospitalId && encounterIds.Contains(o.EncounterId))
                            .Select(o => o.OrderId)
                            .ToListAsync(cancellationToken);
                        await AddLabReportsAsync(context, hospitalId, orderIds, Add, cancellationToken);
                    }
                }
                return new AbdmBuiltRecords { WhenUtc = appointment.ApptDate, Records = records };
            }

            var admission = await _db.Admission.AsNoTracking()
                .Where(a => a.AdmissionId == careContext.SourceId && a.HospitalId == hospitalId)
                .Select(a => new { a.AdmittedAt, a.PrimaryDoctorId })
                .FirstOrDefaultAsync(cancellationToken);
            if (admission == null)
                return null;

            var discharge = await _db.DischargeSummary.AsNoTracking()
                .Where(d => d.AdmissionId == careContext.SourceId && d.HospitalId == hospitalId && d.SignedAt != null)
                .OrderByDescending(d => d.SignedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var (ipdAuthor, ipdPrint) = await AuthorAsync(discharge?.SignedByDoctorId ?? admission.PrimaryDoctorId, discharge?.SignedByDoctorName, age, mobile, cancellationToken);
            var ipdContext = new FhirContext(patient, ipdAuthor, facility, discharge?.SignedAt ?? admission.AdmittedAt, careContext.ReferenceNumber, ipdPrint);

            if (discharge != null && Allowed("DischargeSummary"))
            {
                var medicines = await _db.DischargeMedication.AsNoTracking()
                    .Where(m => m.DischargeSummaryId == discharge.DischargeSummaryId)
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => new FhirMedicine(m.MedicineName, m.Dosage, m.Frequency, m.Durations, m.Route, m.Instructions))
                    .ToListAsync(cancellationToken);
                Add("DischargeSummary", AbdmFhir.DischargeSummary(ipdContext, new FhirDischarge(
                    discharge.ChiefComplaint, discharge.HistoryOfPresentIllness, discharge.CourseInHospital, discharge.ProceduresPerformed,
                    discharge.FinalDiagnosis ?? discharge.AdmittingDiagnosis, discharge.FinalDiagnosisIcd10Code, discharge.FinalDiagnosisIcd10Name,
                    discharge.ConditionAtDischarge, discharge.FollowUpInstructions, discharge.DietInstructions, discharge.ActivityRestrictions, medicines)));
            }

            if (Allowed("DiagnosticReport"))
            {
                var orderIds = await _db.PathologyOrder.AsNoTracking()
                    .Where(o => o.HospitalId == hospitalId && o.AdmissionId == careContext.SourceId)
                    .Select(o => o.OrderId)
                    .ToListAsync(cancellationToken);
                await AddLabReportsAsync(ipdContext, hospitalId, orderIds, Add, cancellationToken);
            }

            if (Allowed("HealthDocumentRecord"))
            {
                var documents = await _db.AdmissionDocument.AsNoTracking()
                    .Where(d => d.AdmissionId == careContext.SourceId && d.HospitalId == hospitalId)
                    .OrderBy(d => d.UploadedAt)
                    .Select(d => new { d.DocumentId, d.DocumentName, d.StorageUrl, d.UploadedAt })
                    .ToListAsync(cancellationToken);
                var files = new List<FhirFile>();
                foreach (var d in documents)
                {
                    var file = await ReadFileAsync(_admissionDocumentsContainer, $"{d.DocumentId}_", d.StorageUrl, d.DocumentName, d.DocumentName, d.UploadedAt, cancellationToken);
                    if (file != null) files.Add(file);
                }
                if (files.Count > 0)
                    Add("HealthDocumentRecord", AbdmFhir.HealthDocument(ipdContext, files));
            }

            if (Allowed("Invoice"))
                await AddInvoicesAsync(ipdContext, hospitalId, careContext.SourceId, "IPD", Add, cancellationToken);
            return new AbdmBuiltRecords { WhenUtc = admission.AdmittedAt, Records = records };
        }

        /// <summary>Each finalised, uncancelled bill raised against this visit's or admission's encounter.</summary>
        private async Task AddInvoicesAsync(FhirContext context, Guid hospitalId, Guid sourceId, string kind, Action<string, System.Text.Json.Nodes.JsonObject> add, CancellationToken cancellationToken)
        {
            var encounterIds = await _db.Encounter.AsNoTracking()
                .Where(e => e.HospitalId == hospitalId && e.SourceId == sourceId)
                .Select(e => e.EncounterId)
                .ToListAsync(cancellationToken);
            if (encounterIds.Count == 0)
                return;
            var invoices = await _db.BillingInvoice.AsNoTracking()
                .Where(i => i.HospitalId == hospitalId && encounterIds.Contains(i.EncounterId) && i.CancelledAt == null && i.FinalizedAt != null)
                .Select(i => new { i.InvoiceId, i.InvoiceNo, i.InvoiceDate })
                .ToListAsync(cancellationToken);
            foreach (var invoice in invoices)
            {
                var lines = await _db.BillingInvoiceChargeEvent.AsNoTracking()
                    .Where(l => l.InvoiceId == invoice.InvoiceId)
                    .Join(_db.BillingChargeEvent.AsNoTracking(), l => l.ChargeEventId, c => c.ChargeEventId, (l, c) => c)
                    .Where(c => c.VoidedAt == null)
                    .Select(c => new FhirInvoiceLine(c.DisplayName ?? "Charge", c.CategoryCode, c.Qty, c.UnitPrice, c.DiscountAmount ?? 0m, c.CgstAmount, c.SgstAmount, c.IgstAmount))
                    .ToListAsync(cancellationToken);
                if (lines.Count == 0)
                    continue;
                var (net, tax) = InvoiceTotals(lines);
                add("Invoice", AbdmFhir.Invoice(context with { WhenUtc = invoice.InvoiceDate },
                    new FhirInvoice(string.IsNullOrWhiteSpace(invoice.InvoiceNo) ? invoice.InvoiceId.ToString() : invoice.InvoiceNo!, kind, lines, net, tax, net + tax)));
            }
        }

        public static (decimal Net, decimal Tax) InvoiceTotals(IReadOnlyCollection<FhirInvoiceLine> lines) =>
            (lines.Sum(l => l.Rate * (l.Quantity <= 0 ? 1 : l.Quantity) - l.Discount), lines.Sum(l => l.Cgst + l.Sgst + l.Igst));

        private async Task<FhirFile?> ReadFileAsync(string container, string keyPrefix, string? storedUrl, string title, string? fileName, DateTime? uploadedAt, CancellationToken cancellationToken)
        {
            if (_files == null || string.IsNullOrEmpty(container))
                return null;
            return await _files.ReadAsync(container, keyPrefix, storedUrl, title, fileName, uploadedAt, cancellationToken);
        }

        /// <summary>The vaccines on a prescription that were actually given. The pad stores a JSON array of
        /// { name, status, date, nextDueDate, doseNumber }; "advised" or "due" ones are not a record of a dose.</summary>
        public static List<FhirVaccine> ParseVaccines(string? json)
        {
            var vaccines = new List<FhirVaccine>();
            if (string.IsNullOrWhiteSpace(json))
                return vaccines;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return vaccines;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    var name = Text(item, "name");
                    var status = Text(item, "status");
                    if (string.IsNullOrWhiteSpace(name) || !WasGiven(status))
                        continue;
                    var dose = Number(item, "doseNumber");
                    vaccines.Add(new FhirVaccine(name!.Trim(), status, DateValue(item, "date"), dose is > 0 ? (int)dose.Value : null, DateValue(item, "nextDueDate")));
                }
            }
            catch (JsonException)
            {
            }
            return vaccines;
        }

        public static bool WasGiven(string? status) => (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" or "given" or "completed" or "complete" or "administered" or "done" or "taken" => true,
            _ => false
        };

        /// <summary>"36 years" from the date of birth, else the age typed at registration.</summary>
        public static string? AgeText(DateTime? dateOfBirth, short? age, string? ageUnit)
        {
            if (dateOfBirth != null)
            {
                var today = DateTime.UtcNow.Date;
                var years = today.Year - dateOfBirth.Value.Year - (today < dateOfBirth.Value.Date.AddYears(today.Year - dateOfBirth.Value.Year) ? 1 : 0);
                if (years >= 1)
                    return $"{years} years";
                var months = (today.Year - dateOfBirth.Value.Year) * 12 + today.Month - dateOfBirth.Value.Month - (today.Day < dateOfBirth.Value.Day ? 1 : 0);
                return months >= 1 ? $"{months} months" : $"{(today - dateOfBirth.Value.Date).Days} days";
            }
            if (age is > 0)
                return $"{age} {(string.IsNullOrWhiteSpace(ageUnit) ? "years" : ageUnit!.Trim().ToLowerInvariant())}";
            return null;
        }

        private static string? JoinLines(params string?[] parts)
        {
            var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
            return kept.Count == 0 ? null : string.Join("\n", kept);
        }

        private static string? Text(JsonElement element, string name)
        {
            var value = Child(element, name);
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static DateTime? DateValue(JsonElement element, string name)
        {
            var text = Text(element, name);
            return !string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;
        }

        private async Task AddLabReportsAsync(FhirContext context, Guid hospitalId, List<Guid> orderIds, Action<string, System.Text.Json.Nodes.JsonObject> add, CancellationToken cancellationToken)
        {
            if (orderIds.Count == 0)
                return;
            var reports = await _db.PathologyReport.AsNoTracking()
                .Where(r => r.HospitalId == hospitalId && orderIds.Contains(r.OrderId))
                .Select(r => new { r.ReportId, r.OrderId, r.ReportNo, r.ApprovedAt, r.GeneratedAt, r.CreatedAt, r.PathologistName, r.PathologistRegNo })
                .ToListAsync(cancellationToken);
            foreach (var report in reports)
            {
                var testIds = await _db.PathologyOrderLine.AsNoTracking()
                    .Where(l => l.OrderId == report.OrderId && l.HospitalId == hospitalId)
                    .Select(l => l.TestId)
                    .ToListAsync(cancellationToken);
                var testNames = await _db.PathologyTestMaster.AsNoTracking()
                    .Where(t => testIds.Contains(t.TestId))
                    .Select(t => t.TestName)
                    .ToListAsync(cancellationToken);
                var interpretations = await _db.PathologyResult.AsNoTracking()
                    .Where(r => r.ReportId == report.ReportId && r.HospitalId == hospitalId && r.Interpretation != null)
                    .Select(r => r.Interpretation!)
                    .ToListAsync(cancellationToken);

                var pathologist = !string.IsNullOrWhiteSpace(report.PathologistName);
                var author = pathologist
                    ? new FhirAuthor(report.ReportId.ToString(), report.PathologistName, report.PathologistRegNo)
                    : context.Author;
                var reportContext = context with
                {
                    Author = author,
                    WhenUtc = report.ApprovedAt ?? report.GeneratedAt ?? report.CreatedAt,
                    Print = context.Print == null ? null : pathologist ? context.Print with { DoctorQualification = null, DoctorSpeciality = "Pathology" } : context.Print
                };
                add("DiagnosticReport", AbdmFhir.DiagnosticReport(reportContext, report.ReportNo, testNames,
                    interpretations.Count == 0 ? null : string.Join("\n", interpretations)));
            }
        }

        /// <summary>The doctor who wrote the record, and what their printed copy shows about them and the patient.</summary>
        private async Task<(FhirAuthor Author, FhirPrint Print)> AuthorAsync(Guid? doctorId, string? knownName, string? age, string? mobile, CancellationToken cancellationToken)
        {
            if (doctorId == null || doctorId == Guid.Empty)
                return (new FhirAuthor("unknown", knownName, null), new FhirPrint(age, mobile, null, null));
            var doctor = await _db.Doctors.AsNoTracking()
                .Where(d => d.DoctorID == doctorId)
                .Select(d => new { d.UserID, d.LicenseNumber, d.Qualification, Speciality = d.PrimaryMedicalSpeciality != null ? d.PrimaryMedicalSpeciality.Name : null })
                .FirstOrDefaultAsync(cancellationToken);
            var name = knownName;
            if (string.IsNullOrWhiteSpace(name) && doctor != null)
                name = await _db.UserProfiles.AsNoTracking().Where(u => u.UserID == doctor.UserID).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
            return (new FhirAuthor(doctorId.Value.ToString(), name, doctor?.LicenseNumber), new FhirPrint(age, mobile, doctor?.Qualification, doctor?.Speciality));
        }

        /// <summary>The vitals JSON saved with an appointment: Bp { Sys, Dia }, Pulse, TempC, Spo2,
        /// HeightCm, WeightKg, Bmi, RespiratoryRate. Key case varies, so it is read loosely.</summary>
        public static FhirVitals? ParseVitals(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;
                var bp = Child(root, "bp");
                return new FhirVitals(
                    bp.ValueKind == JsonValueKind.Object ? Number(bp, "sys") : null,
                    bp.ValueKind == JsonValueKind.Object ? Number(bp, "dia") : null,
                    Number(root, "pulse"), Number(root, "tempC"), Number(root, "spo2"), Number(root, "respiratoryRate"),
                    Number(root, "heightCm"), Number(root, "weightKg"), Number(root, "bmi"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static JsonElement Child(JsonElement element, string name)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return default;
        }

        private static decimal? Number(JsonElement element, string name)
        {
            var value = Child(element, name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                return number;
            return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
    }
}
